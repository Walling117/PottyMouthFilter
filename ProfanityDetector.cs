using NAudio.Wave;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Whisper.net;

namespace CurseWordExtractor
{
    internal static class ProfanityDetector
    {
        public static TimeSpan totalDuration;
        public static async Task<Queue<ProfanityMatch>> DetectProfanity(string whisperAudioFile, HashSet<string> badWords, string modelPath = "ggml-small.en.bin")
        {
            var foundMatches = new Queue<ProfanityMatch>();

            AnsiConsole.MarkupLineInterpolated($"\n\n\t[underline]Loading Model[/]: [bold]{modelPath}[/]");
            using var factory = WhisperFactory.FromPath(modelPath); 
            AnsiConsole.MarkupLine("\t\t[blue]:small_blue_diamond:[/] Building Processor...");
            await using var processor = factory.CreateBuilder().WithLanguage("en").WithProbabilities().WithTokenTimestamps().Build();
            AnsiConsole.MarkupLine("\t\t[blue]:small_blue_diamond:[/] Processor built and loaded");


            using var reader = new WaveFileReader(whisperAudioFile);
            totalDuration = reader.TotalTime;

            int secondsPerChunk = 30;
            int secondsOverlap = 1;

            int bytesPerSecond = reader.WaveFormat.AverageBytesPerSecond;
            int bytesPerChunk = bytesPerSecond * secondsPerChunk;
            int bytesOverlap = bytesPerSecond * secondsOverlap;

            byte[] buffer = new byte[bytesPerChunk];

            AnsiConsole.MarkupLineInterpolated($"\t\t[blue]:small_blue_diamond:[/] Audio Duration: {reader.TotalTime}");
            AnsiConsole.MarkupLineInterpolated($"\t\t[blue]:small_blue_diamond:[/] Processing in {secondsPerChunk}s chunks with {secondsOverlap}s overlap...\n");

            while (reader.Position < reader.Length)
            {
                TimeSpan currentChunkStartTime = reader.CurrentTime;
                int bytesRead = reader.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                int sampleCount = bytesRead / 2;
                float[] pcmData = new float[sampleCount];
                for (int i = 0; i < sampleCount; i++)
                {
                    short sample = BitConverter.ToInt16(buffer, i * 2);
                    pcmData[i] = sample / 32768.0f;
                }

                await foreach (var segment in processor.ProcessAsync(pcmData))
                {
                    AnsiConsole.MarkupLineInterpolated($"\r   [dim]➜ \"{segment.Text.Trim()}\"[/]");

                    // Track Previous Token for Merging
                    string prevTokenText = "";
                    TimeSpan prevTokenStart = TimeSpan.Zero;
                  

                    foreach (var token in segment.Tokens)
                    {
                        string currentText = token.Text;
                        string cleanedCurrent = Helpers.RemovePunctuation(currentText);

                        bool matchFound = false;
                        string matchWord = "";
                        TimeSpan matchStart = TimeSpan.FromMilliseconds(token.Start * 10);
                        TimeSpan matchEnd = TimeSpan.FromMilliseconds(token.End * 10);

                        // Check INDIVIDUAL Token
                        if (badWords.Contains(cleanedCurrent))
                        {
                            matchFound = true;
                            matchWord = cleanedCurrent;
                        }
                        // Check MERGED Token (Previous + Current)
                        //    Example: "fu" + "cker" = "fucker"
                        else
                        {
                            string mergedRaw = prevTokenText + currentText;
                            string mergedClean = Helpers.RemovePunctuation(mergedRaw);

                            if (badWords.Contains(mergedClean))
                            {
                                matchFound = true;
                                matchWord = mergedClean;
                                matchStart = prevTokenStart; // Use start time of the FIRST part
                                                             // matchEnd is already the end time of the current part

                                AnsiConsole.MarkupLineInterpolated($"\r   [#569CD6]❯[/] [bold white]Split Detected:[/] [grey]'{Markup.Escape(prevTokenText)}'[/] [teal]+[/] [grey]'{Markup.Escape(currentText)}'[/] [teal]→[/] [bold red]'{Markup.Escape(matchWord)}'[/]");
                            }
                        }

                        if (matchFound)
                        {
                            TimeSpan relativeStart = matchStart;
                            TimeSpan relativeEnd = matchEnd;

                            TimeSpan actualStart = currentChunkStartTime.Add(relativeStart);
                            TimeSpan actualEnd = currentChunkStartTime.Add(relativeEnd);

                            // Overlap safety check
                            if (relativeStart.TotalSeconds > (secondsPerChunk - secondsOverlap) && reader.Position < reader.Length)
                            {
                                continue;
                            }



                            // If Whisper claims the word took longer than 1.5 seconds, cap it.
                            // This prevents loud noises from dragging out a mute forever.
                            double maxDurationSeconds = 2;
                            if ((actualEnd - actualStart).TotalSeconds > maxDurationSeconds)
                            {
                                actualEnd = actualStart.Add(TimeSpan.FromSeconds(maxDurationSeconds));
                                AnsiConsole.MarkupLineInterpolated($"\r   [#CE9178]❯[/] [bold white]Length Capped:[/] [grey]'{Markup.Escape(matchWord)}' duration reduced to[/] [bold yellow]2.0s[/]");
                            }

                            TimeSpan beepStart = actualStart.Subtract(TimeSpan.FromMilliseconds(200));
                            TimeSpan beepEnd = actualEnd.Add(TimeSpan.FromMilliseconds(400));
                            if (beepStart < TimeSpan.Zero) beepStart = TimeSpan.Zero;

                            AnsiConsole.MarkupLineInterpolated($"\r   [bold #F44747]❯[/] [bold white]Potty word found:[/] [bold #F44747]'{Markup.Escape(matchWord)}'[/] [grey]at[/] [underline #DCDCAA]{actualStart:hh\\:mm\\:ss\\.fff}[/]");

                            foundMatches.Enqueue(new ProfanityMatch
                            {
                                Word = matchWord,
                                Confidence = token.Probability,
                                Start = beepStart,
                                End = beepEnd
                            });
                        }

                        // Store current as previous for the next loop
                        prevTokenText = currentText;
                        prevTokenStart = TimeSpan.FromMilliseconds(token.Start * 10);
                    }
                }

                if (reader.Position < reader.Length)
                {
                    reader.Position -= bytesOverlap;
                }
            }

            return foundMatches;
        }
    }
}