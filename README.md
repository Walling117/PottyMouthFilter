# 🗣️ PottyMouth Filter

**PottyMouth** is a C# CLI tool designed to automatically remove profanity from video files. 
By combining **OpenAI's Whisper** with the millisecond-precision of **Vosk**, it identifies and mutes curse words while preserving the original video quality.
<img width="1085" height="575" alt="image" src="https://github.com/user-attachments/assets/5ed0a7b3-83f8-4670-a8c5-18b73d12c299" />

---

## 📖 The "Why"
I host a **Jellyfin** server for my family to enjoy our personal media collection. Recently, my daughter has started repeating words she hears. 
My wife and I realized we needed a way to enjoy our favorite shows and movies without worrying about "colorful" language being added to our daughter's vocabulary.

---

## Features

* **Hybrid AI Alignment:** Whisper identifies the content, and Vosk refines the timestamps.
* **Audio Muting:** Instead of just lowering volume, it manipulates raw 16-bit PCM bytes to ensure absolute silence during profanity.
* **Deduplication:** Logic to handle overlapping detections and "merged tokens" (catching "fu" + "cker" as one event).
* **CLI:** Powered by `Spectre.Console` for real-time progress bars, status updates, and hit/miss charts.

---

## The Pipeline (How it Works)
1.  **Extracting the Audio:** FFmpeg extracts two versions—a 16kHz mono stream for the AI models and a high-quality stream for the final product.
2.  **Broad Detection (Whisper):** The app chunks audio into 30-second windows. `Whisper.net` scans these for "Potty Words" defined in `CurseWords.txt`.
3.  **Precise Alignment (Vosk):** For every word Whisper finds, the app creates a 10-second search window around it. Vosk then scans to find the exact start and end points.
4.  **The "Janitor" Loop:** The `AudioProcessor` reads the high-quality audio. When it hits a timestamp from the queue, it flips the 16-bit PCM samples to `0` (creating silence) until the word is over.
5.  **Stitching it back together:** The cleaned audio is re-attached to the original video using FFmpeg.
<img width="1446" height="736" alt="Screenshot 2026-03-09 124953" src="https://github.com/user-attachments/assets/9d7e7124-3b9a-4ad6-9aa1-bef1873985b5" />

---

## Tech Stack

| Purpose | Tool | Language |
| :--- | :--- | :--- |
| **Broad AI Detection** | [Whisper.net](https://github.com/alphabetapi/whisper.net) | C# / .NET |
| **AI Timestamp Precision** | [Vosk](https://alphacephei.com/vosk/) | C# / .NET |
| **Audio Byte Manipulation** | [NAudio](https://github.com/naudio/NAudio) | C# / .NET |
| **Video Processing** | [FFmpeg](https://ffmpeg.org/) | CLI |
| **CLI / UI** | [Spectre.Console](https://spectreconsole.net/) | C# / .NET |

---

## Getting Started

### Prerequisites
Before running PottyMouth, make sure you have:
* **FFmpeg:** Must be in your System `PATH`.
* **Whisper Model:** Download `ggml-small.en.bin` and place it in the root folder.
* **Vosk Model:** Place the `vosk-model-en-us-0.22` folder in your build directory.

### Usage
Simply run the executable and pass the path to your video file:

```bash
./PottyMouth.exe "C:\Videos\ActionMovie.mkv"
```

🧠 Things I Learned the Hard Way:
[!TIP]
Context window Problem: Whisper has a small context limit of around 30 seconds. In my testing, I was feeding it small audios. Eventually I fed it a 2 hour audio and it started 
halunicating saying "Thank you!". It was a humorous way to learn that I should be feeding it smaller chunks of audio.

[!IMPORTANT]
16-bit audio has a block alignment rule.
If you start a mute on an odd-numbered byte in a 16-bit audio stream, you get weird glitchy sounds instead of silence. 
Each audio sample is 2 bytes wide, so the mute must start at an even byte boundary. I added block alignment logic to enforce this.

Bigger AI models aren't always worth it.
I tested ggml-medium and ggml-large hoping for better accuracy. The word detection improved slightly, but the CPU and memory usage skyrocketed. 
The ggml-small model paired with Vosk's timestamp refinement ended up being the best balance.

Vosk isn't perfect and that's okay.
Sometimes Whisper detects a word that Vosk simply can't find. For example, Whisper hears the F-bomb, but Vosk processes the audio and transcribes it as "buck in".
When Vosk can't confirm a word, PottyMouth falls back to Whisper's (slightly padded) timestamp as a safety net.

The Merged Token problem.
Sometimes Whisper splits a single bad word into two tokens. 
Whisper is trained on internet data, where people often write words with intentional character breaks to dodge filters ("f***ing" -> Whisper may see "f" and "ing" as separate tokens).
Another reason is because the word "Damnit" is usually split into "damn" and "it." 
To fix this, I added a Merged Token Check that looks ahead to see if the current and previous tokens combine to form a word on the restricted list.

📄 License
This project is for personal/family use. Do whatever you want with it.
