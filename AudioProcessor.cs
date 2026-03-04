using NAudio.Wave;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Text;

namespace CurseWordExtractor
{
    internal static class AudioProcessor
    {
        public static string CensorAudio(Queue<ProfanityMatch> profanityMatches, string inputFile)
        {
            string tempFolder = Path.GetTempPath(); //  this stores in tempFolder given by OS
            string uniqueFileName = "profanityCensor" + Guid.NewGuid().ToString() + ".wav"; // create unique name via GUI so we dont overrite other temp files
            string outputFile = Path.Combine(tempFolder, uniqueFileName);  // learned 'Combine' helps with cross platorm linux vs windows since both use different \/

            try
            {
                // WaveFileReader reads the raw data (bytes)
                using var reader = new WaveFileReader(inputFile);
                using var writer = new WaveFileWriter(outputFile, reader.WaveFormat);


                int blockAlign = reader.WaveFormat.BlockAlign;  // block align is the size of bytes in each audio sample
                int bufferSize = (8192 / blockAlign) * blockAlign; // 8192 is the sweet spot for the wheelbarrow of data
                byte[] buffer = new byte[bufferSize];
                // shows how many bytes make up one second of audio, 
                // regardless of whether it's Mono, Stereo, or 5.1 Surround.
                int bytesPerSecond = reader.WaveFormat.AverageBytesPerSecond;
                long currentBytePosition = 0;
                int bytesRead;

                while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    // my janitor loop cleans up expired badwords that have already been seen
                    while (profanityMatches.Count > 0)
                    {
                        long matchEndByte = (long)(profanityMatches.Peek().End.TotalSeconds * bytesPerSecond);

                        if (currentBytePosition > matchEndByte) // keep dequeing until you find a match that has not expired
                            profanityMatches.Dequeue();
                        else
                            break;
                    }

                    //  Process muting if we are in a bad word zone
                    if (profanityMatches.Count > 0)
                    {
                        var match = profanityMatches.Peek();
                        long matchStartByte = (long)(match.Start.TotalSeconds * bytesPerSecond);
                        long matchEndByte = (long)(match.End.TotalSeconds * bytesPerSecond);

                        for (int i = 0; i < bytesRead; i++)
                        {
                            long absoluteByteIndex = currentBytePosition + i;

                            if (absoluteByteIndex >= matchStartByte && absoluteByteIndex <= matchEndByte)
                            {
                                // Muting a 16-bit PCM track is as simple as flipping the byte to 0
                                buffer[i] = 0;
                            }
                        }
                    }

                    // Write the bytes and advance tracker
                    writer.Write(buffer, 0, bytesRead);
                    currentBytePosition += bytesRead; // keep track on where we are in the audio track
                }

                AnsiConsole.MarkupLine("\t[blue]:small_blue_diamond:[/][white] Successfully Censored audio![/]");
                return outputFile;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[bold red]{Markup.Escape(ex.Message)}[/]");
                AnsiConsole.MarkupLineInterpolated($"[bold red]{Markup.Escape(ex.StackTrace ?? "")}[/]");
                Console.ResetColor();
            }

            return outputFile;
        }

    }
}
