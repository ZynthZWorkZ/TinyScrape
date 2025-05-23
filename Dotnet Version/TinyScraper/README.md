# TinyScraper (.NET Version)

A .NET port of the TinyScraper tool for extracting and playing video streams from tinyzone.org.

## Prerequisites

- .NET 9.0 or later
- Chrome browser installed
- ChromeDriver (compatible with your Chrome version)
- VLC Media Player (optional, for video playback)
- FFplay (optional, for video playback)
- Roku device (optional, for sideloading)

## Setup

1. Clone the repository
2. Navigate to the Dotnet Version/TinyScraper directory
3. Create a `movie_links.txt` file in the project directory with the following format:
   ```
   2023 | Movie Title | https://ww3.tinyzone.org/movie/movie-title-123456/
   ```
4. Build the project:
   ```
   dotnet build
   ```

## Usage

### Basic Commands

1. Search and play in VLC:
   ```
   dotnet run --search "movie title" --vlc
   ```

2. Search and play in FFplay:
   ```
   dotnet run --search "movie title" --ffplay
   ```

3. Watch full video in FFplay:
   ```
   dotnet run --search "movie title" --ffplay --w
   ```

4. Run in visible mode (not headless):
   ```
   dotnet run --search "movie title" --head
   ```

5. Random movie selection:
   ```
   dotnet run --rw --vlc
   ```

6. Process specific URL:
   ```
   dotnet run "https://ww3.tinyzone.org/movie/example-123456/" --vlc
   ```

### Roku Sideloading

1. Search and sideload to Roku:
   ```
   dotnet run --search "movie title" --rokusl "192.168.1.100"
   ```

2. Process URL and sideload to Roku:
   ```
   dotnet run "https://ww3.tinyzone.org/movie/example-123456/" --rokusl "192.168.1.100"
   ```

3. Random movie with Roku sideloading:
   ```
   dotnet run --rw --rokusl "192.168.1.100"
   ```

### Command Line Arguments

- `--search` or `-s`: Search for movies in movie_links.txt
- `--vlc` or `-VLC`: Open video in VLC player
- `--ffplay` or `-FFPLAY`: Open video in FFplay
- `--w` or `-w`: Watch full video in FFplay (use with --ffplay)
- `--head` or `-Head`: Run browser in visible mode
- `--rw` or `-RW`: Select and play a random movie
- `--rokusl` or `-RokuSL`: Create and sideload a Roku app (requires Roku device IP)

### Combined Options

You can combine multiple options as needed:
```
dotnet run --search "movie title" --vlc --ffplay --w --head
```

## File Format

The `movie_links.txt` file should contain one movie per line in the following format:
```
year | title | url
```

Example:
```
2023 | The Matrix | https://ww3.tinyzone.org/movie/the-matrix-123456/
2022 | Inception | https://ww3.tinyzone.org/movie/inception-789012/
```

## Notes

- The search is case-insensitive
- URLs must be from tinyzone.org (https://ww3.tinyzone.org/)
- For Roku sideloading, you need the correct IP address of your Roku device
- The program will create a log file (tinyzone.log) with detailed information about its operation
- When using `--head`, the browser window will be visible, which can be helpful for debugging

## Troubleshooting

1. If videos don't play:
   - Make sure VLC or FFplay is installed and in your system PATH
   - Try running with `--head` to see what's happening in the browser

2. If Roku sideloading fails:
   - Verify your Roku device IP address
   - Make sure your computer and Roku are on the same network
   - Check the Roku device's developer settings

3. If no movies are found:
   - Check that movie_links.txt exists and has the correct format
   - Verify that the search term matches movie titles in the file

## Logging

The program creates a log file (tinyzone.log) that contains detailed information about:
- Network requests
- Found URLs
- Play button interactions
- Video playback attempts
- Any errors that occur

Check this file if you encounter any issues.

## License

This project is licensed under the MIT License - see the LICENSE file for details. 