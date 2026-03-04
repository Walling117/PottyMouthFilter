using Spectre.Console;
using System;
using System.Diagnostics;

namespace CurseWordExtractor
{
    internal static class ExtractAudio
    {
        public static string GetAudio16khz(string videoFilePath)
        {
            string outputWavPath = "temp_16khz_audio.wav";

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "ffmpeg";

            // Added aresample=async=1:first_pts=0 to force perfect synchronization 
            // and pad any missing start-time gaps with silence.
            psi.Arguments = $"-i \"{videoFilePath}\" -vn -ar 16000 -ac 1 -c:a pcm_s16le -af \"aresample=async=1:first_pts=0\" -y \"{outputWavPath}\"";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            using (Process process = Process.Start(psi))
            {
                process.WaitForExit();
            }

            AnsiConsole.MarkupLine("\n\t\t[blue]:small_blue_diamond:[/]16kHz Audio extraction complete.");
            return outputWavPath;
        }

        public static string GetHighQualityAudio(string videoFilePath)
        {
            string outputWavPath = "temp_hq_audio.wav";

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "ffmpeg";

            // Added first_pts=0 here as well so the high-quality audio aligns perfectly 
            // with the 16kHz audio and the original MKV container.
            psi.Arguments = $"-i \"{videoFilePath}\" -vn -c:a pcm_s16le -af \"aresample=async=1:first_pts=0\" -y \"{outputWavPath}\"";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            using (Process process = Process.Start(psi))
            {
                process.WaitForExit();
            }

            AnsiConsole.MarkupLine("\n\t\t[blue]:small_blue_diamond:[/]High-Quality extraction complete.");
            return outputWavPath;
        }
    }
}