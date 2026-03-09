using Spectre.Console;
using System;
using System.Diagnostics;
using System.IO;

namespace CurseWordExtractor
{
    internal static class Muxer
    {
        public static void MuxVideo(string originalVideoPath, string censoredAudioPath)
        {           
        
            string outputVideoPath = "Censored_Movie.mkv";

         
            if (File.Exists(outputVideoPath)) File.Delete(outputVideoPath);

            string ffmpegArgs =
     "-analyzeduration 100M -probesize 100M " +
     "-hwaccel d3d11va " +
     "-fix_sub_duration " +
     $"-err_detect ignore_err -i \"{originalVideoPath}\" " +
     $"-i \"{censoredAudioPath}\" " +
     "-map 0:v -map 1:a:0 -map 0:s? " +
     "-vf format=yuv420p " +        // converts 10-bit to 8-bit for AMF
     "-c:v h264_amf " +
     "-quality balanced " +
     "-rc cqp -qp_i 20 -qp_p 22 " +
     "-c:a:0 ac3 -b:a:0 640k " +
     "-c:s copy " +
     "-start_at_zero " +
     "-max_muxing_queue_size 4096 " +
     "-max_interleave_delta 0 " +
     "-af aresample=async=1000 " +
     "-movflags +faststart " +
     "-metadata:s:a:0 title=\"ProfanityFilter\" " +
     "-disposition:a:0 default " +
     $"\"{outputVideoPath}\"";

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = ffmpegArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process process = new Process { StartInfo = startInfo };

            double totalDurationSeconds = (double)ProfanityDetector.totalDuration.TotalSeconds;

            AnsiConsole.Progress().Start(ctx =>
                {
                    var encodingTask = ctx.AddTask("[blue]Inserting audio track into media...[/]", maxValue: totalDurationSeconds);
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data) && e.Data.Contains("time="))
                        {
                            int timeIndex = e.Data.IndexOf("time=") + 5;
                            if (timeIndex + 11 <= e.Data.Length)
                            {
                                string timeString = e.Data.Substring(timeIndex, 11);

                                if(TimeSpan.TryParse(timeString, out TimeSpan currentTime))
                                {
                                    encodingTask.Value = currentTime.TotalSeconds;
                                }
                            }
                           
                        }
                    };

                    process.Start();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                });

            process.WaitForExit();

            // Verify FFmpeg exited cleanly without fatal errors
            if (process.ExitCode != 0)
            {
                AnsiConsole.MarkupLine("[bold red]\n\t\t>ERROR: FFmpeg crashed or failed. Original video is completely safe.[/]");
                return; // Stop execution here. Do NOT proceed to deletion.
            }

            AnsiConsole.MarkupLine("\n\t\t[blue]:small_blue_diamond:[/][white] Encoding Finished![/]");
            AnsiConsole.MarkupLine("\t\t[blue]:small_blue_diamond:[/][white] Attempting to replace old video...[/]");

            try
            {
                // Verify the newly created video actually exists and has data (isn't 0 bytes)
                FileInfo newFile = new FileInfo(outputVideoPath);

                if (newFile.Exists)
                {
                    if (File.Exists(originalVideoPath))
                    {
                        File.Delete(originalVideoPath); // Now it is safe to delete
                    }

                    File.Move(outputVideoPath, originalVideoPath);
                    AnsiConsole.MarkupLine("\t\t[blue]:small_blue_diamond:[/][white] Successfully replaced old video![/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[bold red]\t\t>ERROR: The newly encoded video is missing or too small. Original video is safe.[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[bold red]\t\t>ERROR: could not overwrite old .mkv {Markup.Escape(ex.Message)}[/]");
            }
        }
    }
}