using System;
using System.Collections.Generic;
using System.Text;
using NAudio.Wave;
using System.Text.Json;
using Vosk;
using System.Diagnostics.CodeAnalysis;
using Spectre;
using Spectre.Console;
namespace CurseWordExtractor
{
    internal static class PreciseAligner
    {
        public static Queue<ProfanityMatch> AlignTimeStamps(Queue<ProfanityMatch> whisperMatches, string audioFilePath, HashSet<string> badWords, string modelPath = "vosk-model-en-us-0.22")
        {
            AnsiConsole.MarkupLine("\n\t[bold yellow][underline]\nStarting Precise Aligner via Vosk[/][/]");

            var perfectlyAlignedMatches = new Queue<ProfanityMatch>();

            // keep track of used timestamps
            var usedTimestamps = new List<TimeSpan>();
            // Turning off Vosk printing a bunch of messy logs to console
            Vosk.Vosk.SetLogLevel(-1);

            // Load Vosk into memory
            AnsiConsole.Markup("\t\t>[yellow]Attempting to load Vosk...[/]");
            using var model = new Model(modelPath);

            // Open audio with NAduio to read specific bytes
            using var reader = new WaveFileReader(audioFilePath);
            int bytesPerSecond = reader.WaveFormat.AverageBytesPerSecond;
            while (whisperMatches.Count > 0)
            {

                // the audio is 16,000Hz
                using var recognizer = new VoskRecognizer(model, 16000.0f); // create it fresh with each loop to reset stopwatch
                recognizer.SetWords(true); // please output words

                
                var match = whisperMatches.Dequeue();

                // Setup time windows. Since whisper can be off by 2 seconds in either direciton
                TimeSpan windowStart = match.Start.Subtract(TimeSpan.FromSeconds(5));
                TimeSpan windowEnd = match.End.Add(TimeSpan.FromSeconds(5));

                // convert time to bytes. SO the needle is placed in the right spot on audio track
                long startByte = (long)(windowStart.TotalSeconds * bytesPerSecond);
                long endByte = (long)(windowEnd.TotalSeconds * bytesPerSecond);

                // CRITICAL C# AUDIO MATH: Block Alignment!!!!!!!!!!!!!!
                // A 16-bit audio sample is 2 bytes long. If we accidentally start reading on an odd number 
                // (like byte 1001), we slice a sound wave in half, which creates horrible static and crashes Vosk.
                // This forces our start and end points to snap to the nearest even boundary.
                startByte -= startByte % reader.WaveFormat.BlockAlign;
                endByte -= endByte % reader.WaveFormat.BlockAlign;


                long bytesToRead = endByte - startByte;

                // move our needle and get bytes
                reader.Position = startByte;
                byte[] snippetData = new byte[bytesToRead];
                int bytesRead = reader.Read(snippetData, 0, (int)bytesToRead);

                // pass 4 seconds to Vosk
                recognizer.AcceptWaveform(snippetData, bytesRead);


                // -------------------------Read Output-----------------------------
                string jsonResult = recognizer.FinalResult();
                try
                {
                    // open JSON document 
                    using JsonDocument doc = JsonDocument.Parse(jsonResult);
                    JsonElement root = doc.RootElement;

                    // 1. Declare this OUTSIDE the check
                    bool wordConfirmed = false;

                    if (root.TryGetProperty("result", out JsonElement resultElement))
                    {
                        // go through every word Vosk heard in 4 second window
                        foreach (JsonElement wordObj in resultElement.EnumerateArray())
                        {
                            string voskWord = wordObj.GetProperty("word").GetString() ?? "";
                            string voskCleanedWord = Helpers.RemovePunctuation(voskWord.ToLower());
                            string whisperTargetWord = Helpers.RemovePunctuation(match.Word.ToLower());

                            if (badWords.Contains(voskCleanedWord))
                            {
                                // See if word matches whisper's word
                                if (voskCleanedWord.Contains(whisperTargetWord) || whisperTargetWord.Contains(voskCleanedWord))
                                {
                                    double relativeStart = wordObj.GetProperty("start").GetDouble();
                                    double relativeEnd = wordObj.GetProperty("end").GetDouble();
                                    double confidence = wordObj.GetProperty("conf").GetDouble();

                                    // Add Vosk time to window start and convert to TimeSpan
                                    TimeSpan preciseStart = windowStart.Add(TimeSpan.FromSeconds(relativeStart));
                                    TimeSpan preciseEnd = windowStart.Add(TimeSpan.FromSeconds(relativeEnd));
                                    
                                    // --- THE LOOK PAST RULE ---
                                    bool alreadyUsed = false;
                                    foreach (var usedTime in usedTimestamps)
                                    {
                                        // If this Vosk word starts within 400ms of a word we already processed, it's a duplicate. 
                                        //400ms because that is the usual time it takes for a word to be said
                                        if (Math.Abs((preciseStart - usedTime).TotalMilliseconds) < 400)
                                        {
                                            alreadyUsed = true;
                                            break;
                                        }
                                    }

                                    if (alreadyUsed)
                                    {
                                        // Skip this word and keep reading the JSON array!
                                        continue;
                                    }

                                    // Mark this exact time as "used" so we never lock onto it again
                                    usedTimestamps.Add(preciseStart);
                                    // Padding 
                                    TimeSpan beepStart = preciseStart.Subtract(TimeSpan.FromMilliseconds(200));
                                    TimeSpan beepEnd = preciseEnd.Add(TimeSpan.FromMilliseconds(200));

                                    AnsiConsole.MarkupLineInterpolated($"[green]\t✓ Vosk validated '{Markup.Escape(voskWord)}' | Whisper: {match.Start:hh\\:mm\\:ss\\.fff} -> Vosk: {beepStart:hh\\:mm\\:ss\\.fff}[/]");
                                    
                                    perfectlyAlignedMatches.Enqueue(new ProfanityMatch
                                    {
                                        Word = match.Word,
                                        Start = beepStart,
                                        End = beepEnd,
                                        Confidence = confidence
                                    });

                                    wordConfirmed = true;
                                    break;
                                }
                            }
                        }
                    }

                    // 2. Move the Fallback OUTSIDE the check
                    if (!wordConfirmed)
                    {
                        string fullText = root.GetProperty("text").GetString() ?? "";

                        AnsiConsole.MarkupLineInterpolated($"[#DCDCAA]⚠ Vosk missed — Whisper expected '{Markup.Escape(match.Word)}' at {match.Start:hh\\:mm\\:ss\\.fff}[/]");
                        AnsiConsole.MarkupInterpolated($"\t[#DCDCAA]   --> Vosk heard: \"{Markup.Escape(fullText)}\"[/]");
                        AnsiConsole.MarkupLine($"\t[#DCDCAA]   --> FALLING BACK to Whisper's padded timestamp.[/]");

                        // Vosk missed it (probably due to noise or autocorrecting to "funky").
                        // We use Whisper's original time, but apply a wide 1-second pad 
                        // to act as a "safety net" against Whisper's time drift.
                        TimeSpan fallbackStart = match.Start.Subtract(TimeSpan.FromSeconds(1));
                        TimeSpan fallbackEnd = match.End.Add(TimeSpan.FromSeconds(1));

                        if (fallbackStart < TimeSpan.Zero) fallbackStart = TimeSpan.Zero;

                        perfectlyAlignedMatches.Enqueue(new ProfanityMatch
                        {
                            Word = match.Word,
                            Start = fallbackStart,
                            End = fallbackEnd,
                            Confidence = match.Confidence
                        });
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLineInterpolated($"[bold red]✗ Error:[/] Error reading JSON --> {Markup.Escape(ex.Message)}");
                }

            }

            return perfectlyAlignedMatches;
        } 
    }
}
