using System;
using System.Collections.Generic;
using System.Text;

namespace CurseWordExtractor
{
    internal class ProfanityMatch
    {
        public string Word {  get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }

        public double Confidence { get; set; } // how confident is Whisper that it properly heard the word

        public ProfanityMatch() { }
        public ProfanityMatch(string word, TimeSpan Start, TimeSpan End, double Confidence)
        {
            this.Word = word;
            this.Start = Start;
            this.End = End;
            this.Confidence = Confidence;
        }

    }
}
