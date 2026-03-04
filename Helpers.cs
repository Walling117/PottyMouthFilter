using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Text;

namespace CurseWordExtractor
{
    internal static class Helpers
    {

        public static string RemovePunctuation(string word)
        {
            StringBuilder cleanString = new StringBuilder();
            foreach (char c in word)
            {
                if (char.IsLetterOrDigit(c))
                    cleanString.Append(c);
            }
            return cleanString.ToString();
        }

        public static Queue<ProfanityMatch> DeduplicateMatches(Queue<ProfanityMatch> input)
        {
            // Sort all matches by their Start Time
            var sorted = input.OrderBy(m => m.Start).ToList();
            var result = new List<ProfanityMatch>();

            if (sorted.Count == 0) return new Queue<ProfanityMatch>();

            var current = sorted[0];

            for (int i = 1; i < sorted.Count; i++)
            {
                var next = sorted[i];

                // Overlap Check: Do these words start within 500 milliseconds of each other?
                bool isDuplicateEvent = Math.Abs((next.Start - current.Start).TotalMilliseconds) < 500;

                if (isDuplicateEvent)
                {
                    // If they are duplicates, keep the one with the higher confidence score
                    // or the one that is a longer word (e.g., "fucking" beats "fuck")
                    if (next.Word.Length > current.Word.Length)
                    {
                        current = next;
                    }
                    else if (next.Word.Length == current.Word.Length && next.Confidence > current.Confidence)
                    {
                        current = next;
                    }
                    // Otherwise keep current and ignore next
                }
                else
                {
                    // They are distinct words spoken at different times. Save the current one and move on.
                    result.Add(current);
                    current = next;
                }
            }

            // Ensure the very last word is added to the list
            result.Add(current);

            return new Queue<ProfanityMatch>(result);
        }

        public static HashSet<string> GetCurseWordList(string filePath = "CurseWords.txt")
        {
            HashSet<string> badWordList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(filePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                AnsiConsole.MarkupInterpolated($"\t[bold]Error could not find[/] filePath");
                Console.ResetColor();
                return badWordList;
            }
            using (StreamReader curseWordsReader = new StreamReader(filePath))
            {
                string curseWord;

                while ((curseWord = curseWordsReader.ReadLine()) != null)
                {
                    curseWord = curseWord.Trim();
                    if(!string.IsNullOrWhiteSpace(curseWord))
                    badWordList.Add(curseWord.Trim());
                }
                AnsiConsole.Write(new Text("\t\tLoaded!", new Style(foreground: Color.Yellow)));

                return badWordList;
            }
        }
        public static void CleanUp(string whisperAudioFile, string highQualityAudioFile, string profanityFreeAudioPath)
        {
            AnsiConsole.MarkupLine("[#DCDCAA]\t---Starting file cleaning---[/]");

            // Put them in an array so you don't have to copy/paste the logic three times
            string[] filesToDelete = { whisperAudioFile, highQualityAudioFile, profanityFreeAudioPath };

            foreach (string file in filesToDelete)
            {
                if (!string.IsNullOrEmpty(file) && File.Exists(file))
                {
                    try
                    {
                        File.Delete(file);
                        AnsiConsole.MarkupLineInterpolated($"[#DCDCAA]\t\t[blue]:small_blue_diamond:[/]Deleted {Markup.Escape(file)}[/]");
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"\t\t>>>[red bold]Could not delete {file}: {ex.Message}[/]");
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                    }
                }
            }
            AnsiConsole.MarkupLine("[#DCDCAA bold]\tCleanup Complete[/]");
            Console.ResetColor();
        }
        
    }
}
