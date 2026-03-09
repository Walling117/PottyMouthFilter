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

                // Overlap Check Do these words start within 500 milliseconds of each other?
                bool isCloseInTime = Math.Abs((next.Start - current.Start).TotalMilliseconds) < 500; // 500 milliseconds is 

                // Only treat as a duplicate if it's also the same word (or a substring match)
                bool isSameWord = string.Equals(current.Word, next.Word, StringComparison.OrdinalIgnoreCase)
                               || current.Word.Contains(next.Word, StringComparison.OrdinalIgnoreCase)
                               || next.Word.Contains(current.Word, StringComparison.OrdinalIgnoreCase);

                bool isDuplicateEvent = isCloseInTime && isSameWord;

                if (isDuplicateEvent)
                {
                    // If they are duplicates, keep the one with the higher confidence score
                    // or the one that is a longer word ("fucking" beats "fuck")
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
                AnsiConsole.MarkupLineInterpolated($"\t[bold]Error could not find[/] {Markup.Escape(filePath)}");
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
                return badWordList;
            }
        }
        public static void CleanUp(string whisperAudioFile, string highQualityAudioFile, string profanityFreeAudioPath)
        {
            AnsiConsole.MarkupLine("\n\n[yellow]:large_orange_diamond:[/][bold yellow] Starting file cleaning[/]");

            
            string[] filesToDelete = { whisperAudioFile, highQualityAudioFile, profanityFreeAudioPath };

            foreach (string file in filesToDelete)
            {
                if (!string.IsNullOrEmpty(file) && File.Exists(file))
                {
                    try
                    {
                        File.Delete(file);
                        AnsiConsole.MarkupLineInterpolated($"\t\t[blue]:small_blue_diamond:[/][bold white]Deleted[/][white] {Markup.Escape(file)}[/]");
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLineInterpolated($"[bold red]\t\t>>>Could not delete {Markup.Escape(file)}: {Markup.Escape(ex.Message)}[/]");
                    }
                }
            }
            AnsiConsole.MarkupLine("[bold white]\t\t[blue]:small_blue_diamond:[/]Cleanup Complete[/]");
        }
        
    }
}
