# TinyZone Movie Stream Extractor

This Python script automates the extraction and playback of streaming video links (specifically `.m3u8` streams) from [tinyzone.org](https://ww3.tinyzone.org/) movie pages. It can open these streams in VLC or FFplay, and even sideload them as a Roku app for direct playback on Roku devices.

## Features

- **Extracts m3u8 video stream URLs** from TinyZone movie pages.
- **Automates browser actions** (using Selenium) to bypass play buttons and anti-bot measures.
- **Plays extracted streams** in VLC or FFplay.
- **Random movie selection** and search from a local movie list.
- **Roku sideloading:** Automatically creates and uploads a Roku app to play the selected movie stream.
- **Headless or visible browser** operation.
- **Logging** to both file and console for troubleshooting.

## Requirements

- Python 3.7+
- Google Chrome browser
- [ChromeDriver](https://chromedriver.chromium.org/) (automatically managed)
- VLC media player (for VLC playback)
- FFmpeg/FFplay (for FFplay playback) - **FFmpeg must be installed and in your system PATH**
- [Selenium](https://pypi.org/project/selenium/)
- [webdriver-manager](https://pypi.org/project/webdriver-manager/)
- [BeautifulSoup4](https://pypi.org/project/beautifulsoup4/)
- [requests](https://pypi.org/project/requests/)

Install dependencies with:

```bash
pip install selenium webdriver-manager beautifulsoup4 requests
```

## Usage

### 1. Extract and Play a Movie from TinyZone

```bash
python TinyZone.py "https://ww3.tinyzone.org/movie/example-123456/"
```

- Replace the URL with the actual TinyZone movie page URL.

#### Optional Flags

- `-VLC` or `--vlc`: Play the extracted video in VLC.
- `-FFPLAY` or `--ffplay`: Play the extracted video in FFplay.
- `-w` or `--w`: Watch the full video in FFplay (use with `-FFPLAY`).
- `-Head` or `--head`: Run the browser in visible (non-headless) mode for debugging.
- `-RokuSL <ROKU_IP>` or `--rokusl <ROKU_IP>`: Sideload the movie as a Roku app to the specified Roku device IP.

**Example:**

```bash
python TinyZone.py "https://ww3.tinyzone.org/movie/example-123456/" -VLC
```

### 2. Search and Play from a Local Movie List

If you have a `movie_links.txt` file (one movie per line, format: `year|title|url`), you can search and play movies:

```bash
python TinyZone.py -S "search term"
```

- Use `-RW` or `--rw` to pick a random movie.
- Combine with other flags as needed.

**Example:**

```bash
python TinyZone.py -S "Inception" -VLC
```

**Random Movie Example:**

```bash
python TinyZone.py -RW
```

### 3. Sideload to Roku

To create and upload a Roku app for a movie:

```bash
python TinyZone.py "https://ww3.tinyzone.org/movie/example-123456/" -RokuSL <ROKU_IP>
```

- Replace `<ROKU_IP>` with your Roku device's IP address.

**Example:**

```bash
python TinyZone.py "https://ww3.tinyzone.org/movie/example-123456/" -RokuSL 192.168.1.100
```

### 4. Run in Visible Mode

To run the script with the browser in visible mode (non-headless):

```bash
python TinyZone.py "https://ww3.tinyzone.org/movie/example-123456/" -Head
```

### 5. Play in FFplay with Full Video

To play the extracted video in FFplay and watch the full video:

```bash
python TinyZone.py "https://ww3.tinyzone.org/movie/example-123456/" -FFPLAY -w
```

### 6. Refresh Movie Links

To scrape all movies from the TinyZone website and refresh your `movie_links.txt` file, use the `Pages.py` script:

```bash
python Pages.py
```

- This will update the `movie_links.txt` file with the latest movie listings from the website.

## Troubleshooting

- Most issues can be resolved by running the script with the `-Head` flag. This opens the browser in visible mode, allowing you to see what's happening and interact with the page if needed.
- Ensure VLC or FFplay is installed and in your system PATH.
- For Roku sideloading, make sure your device is in developer mode and accessible on your network.

## Disclaimer

This script is for educational purposes only. Respect the terms of service of any streaming site you use. Do not use this tool to infringe on copyrights or access content illegally. 