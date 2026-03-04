using NAudio.Mixer;
using NAudio.Wave;
using Spectre.Console;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;
namespace CurseWordExtractor
{
    internal class Program
    {

        static async Task Main(string[] args)
        {
            // This is for Spectre to force windows to use Unicode for output
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;
            Console.Clear();

            if (args.Length == 0 || (!File.Exists(args[0]))) // check user argument
            {
                AnsiConsole.MarkupLineInterpolated($"[bold red]✗ Error:[/] Please enter a valid file path!");
                return;
            }

            string originalFile = args[0];
            string profanityFreeAudioPath = string.Empty;
            string highQualityAudioFile = string.Empty;
            string whisperAudioFile = string.Empty;

            try
            {
                await AnsiConsole.Status().Spinner(Spinner.Known.Circle).StartAsync("Booting up PottyMouth filter :)...", async ctx =>
                {
                    AnsiConsole.MarkupLine("[bold yellow]Loading CurseWords.txt [/]");
                    ctx.Status("[bold yellow]Loading CurseWords.txt[/]");
                    HashSet<string> badWords = Helpers.GetCurseWordList();
                    AnsiConsole.MarkupLine("\t\t[blue]:small_blue_diamond:[/] CurseWords.txt loaded");

                    ctx.Status("[bold yellow]Extracting 16kHz audio...[/]");
                    whisperAudioFile = ExtractAudio.GetAudio16khz(originalFile);

                    // Run Whisper
                    ctx.Status("[bold yellow]Detecting profanity via Whisper...[/]");
                    Queue<ProfanityMatch> rawMatches;
                    rawMatches = await ProfanityDetector.DetectProfanity(whisperAudioFile, badWords);

                    // strip duplicates
                    rawMatches = Helpers.DeduplicateMatches(rawMatches);

                    ctx.Status("[bold yellow]Correcting timestamps with Vosk...[/]");
                    // Vosk
                    Queue<ProfanityMatch> alignedMatches = PreciseAligner.AlignTimeStamps(rawMatches, whisperAudioFile, badWords);
                    // take the sorted items off the table and pour them straight into a new Queue
                    Queue<ProfanityMatch> finalSortedMatches = new Queue<ProfanityMatch>(alignedMatches.OrderBy(m => m.Start));
                    string reportFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Censored_Timestamps.txt");

                    ctx.Status($"[#DCDCAA]Exporting {finalSortedMatches.Count} timestamps to: {reportFile}[/]");

                    using (StreamWriter writer = new StreamWriter(reportFile))
                    {
                        writer.WriteLine("PROFANITY TIMESTAMP REPORT");
                        writer.WriteLine($"Date: {DateTime.Now}");
                        writer.WriteLine($"Total Detections: {finalSortedMatches.Count}");
                        writer.WriteLine("----------------------------------------------------------------------");
                        // {0,-16} = Start takes 16 chars, left aligned.
                        writer.WriteLine("{0,-16} | {1,-16} | {2,-15} | {3}", "START", "END", "WORD", "CONFIDENCE");
                        writer.WriteLine("----------------------------------------------------------------------");

                        foreach (var match in finalSortedMatches)
                        {
                            writer.WriteLine("{0,-16} | {1,-16} | {2,-15} | {3:P1}",
                            match.Start.ToString(@"hh\:mm\:ss\.fff"),
                            match.End.ToString(@"hh\:mm\:ss\.fff"),
                            match.Word,
                            match.Confidence);
                        }
                    }


                    ctx.Status("[bold yellow]Extracting high-quality audio...[/]");
                    highQualityAudioFile = ExtractAudio.GetHighQualityAudio(originalFile);

                    ctx.Status("[bold yellow]Censoring audio...[/]");
                    profanityFreeAudioPath = AudioProcessor.CensorAudio(finalSortedMatches, highQualityAudioFile);
                });

                // Muxing runs OUTSIDE Status so its Progress bar doesn't conflict
                Muxer.MuxVideo(originalFile, profanityFreeAudioPath);

                AnsiConsole.Write(new Panel(new Markup("[bold #4EC9B0]✔     Profanity filter applied successfully![/]")).Border(BoxBorder.Rounded).BorderColor(Color.Teal).Padding(1, 0, 1, 0));
            }            
            catch (Exception ex) { AnsiConsole.MarkupLineInterpolated($"[#DCDCAA]{Markup.Escape(ex.Message)}[/]"); }
            finally
            {
                Helpers.CleanUp(whisperAudioFile, highQualityAudioFile, profanityFreeAudioPath);
                AnsiConsole.MarkupLineInterpolated($"[bold yellow]Potty Mouth Filter finished at [/][underline][white]{DateTime.Now:HH:mm:ss}[/][/]");
            }

        }
    }
}
