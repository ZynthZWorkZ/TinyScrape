from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.chrome.service import Service
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC
from selenium.webdriver.common.action_chains import ActionChains
import time
import os
import tempfile
import logging
import json
import argparse
import requests
from bs4 import BeautifulSoup
import urllib.request
import urllib.error
import subprocess
import webbrowser
import random
import shutil
from webdriver_manager.chrome import ChromeDriverManager

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('tinyzone.log'),
        logging.StreamHandler()
    ]
)

def validate_tinyzone_url(url):
    """Validate if the URL is from tinyzone.org"""
    if not url.startswith('https://ww3.tinyzone.org/'):
        raise argparse.ArgumentTypeError("URL must be from tinyzone.org (https://ww3.tinyzone.org/)")
    return url

def open_in_default_browser(url):
    """Open URL in user's default browser."""
    try:
        logging.info(f"Opening URL in default browser: {url}")
        webbrowser.open(url)
        time.sleep(5)  # Wait for 5 seconds
        return True
    except Exception as e:
        logging.error(f"Error opening URL in default browser: {str(e)}")
        return False

def check_cloudnestra_play_button(driver, url, headless=True):
    """Check if play button exists on cloudnestra.com page and click it if found."""
    # Reset the found_m3u8 flag
    check_cloudnestra_play_button.found_m3u8 = False
    check_cloudnestra_play_button.m3u8_urls = set()  # Store m3u8 URLs
    
    try:
        logging.info(f"Navigating to cloudnestra URL: {url}")
        
        # If not headless, open URL in default browser first and exit
        if not headless:
            if open_in_default_browser(url):
                logging.info("Headless issues should be fixed now. Please run the script again without -Head flag.")
                return False
            return False
        
        driver.get(url)
        time.sleep(15)  # Wait for page load
        
        # Wait for the play button
        wait = WebDriverWait(driver, 10)
        try:
            play_button = wait.until(EC.presence_of_element_located((By.CSS_SELECTOR, "#pl_but.fas.fa-play")))
            logging.info("Found play button on cloudnestra page!")
            
            # Try to click the play button
            try:
                # Scroll the element into view
                driver.execute_script("arguments[0].scrollIntoView(true);", play_button)
                time.sleep(1)  # Wait for scroll to complete
                
                # Try different click methods
                try:
                    # Method 1: Direct click
                    play_button.click()
                    logging.info("Successfully clicked play button using direct click")
                except:
                    try:
                        # Method 2: JavaScript click
                        driver.execute_script("arguments[0].click();", play_button)
                        logging.info("Successfully clicked play button using JavaScript")
                    except:
                        # Method 3: Action Chains
                        ActionChains(driver).move_to_element(play_button).click().perform()
                        logging.info("Successfully clicked play button using Action Chains")
                
                # Wait for any new requests after clicking
                time.sleep(5)
                
                # Look for m3u8 URLs in network traffic
                logs = driver.get_log('performance')
                for entry in logs:
                    try:
                        log = json.loads(entry['message'])['message']
                        if 'Network.responseReceived' in log['method']:
                            response_url = log['params']['response']['url']
                            if '.m3u8' in response_url:
                                check_cloudnestra_play_button.m3u8_urls.add(response_url)
                                logging.info(f"Found m3u8 URL: {response_url}")
                    except:
                        continue
                
                # Check iframes for m3u8 URLs
                iframes = driver.find_elements(By.TAG_NAME, "iframe")
                for iframe in iframes:
                    try:
                        driver.switch_to.frame(iframe)
                        time.sleep(2)
                        
                        iframe_logs = driver.get_log('performance')
                        for entry in iframe_logs:
                            try:
                                log = json.loads(entry['message'])['message']
                                if 'Network.responseReceived' in log['method']:
                                    response_url = log['params']['response']['url']
                                    if '.m3u8' in response_url:
                                        check_cloudnestra_play_button.m3u8_urls.add(response_url)
                                        logging.info(f"Found m3u8 URL in iframe: {response_url}")
                            except:
                                continue
                        
                        driver.switch_to.default_content()
                    except:
                        continue
                
                if check_cloudnestra_play_button.m3u8_urls:
                    logging.info("\nFound m3u8 URLs after clicking play button:")
                    for m3u8_url in check_cloudnestra_play_button.m3u8_urls:
                        logging.info(f"- {m3u8_url}")
                    
                    # Set the flag to indicate we found m3u8 URLs
                    check_cloudnestra_play_button.found_m3u8 = True
                    return True  # Return immediately after finding m3u8 URLs
                else:
                    logging.info("No m3u8 URLs found after clicking play button")
                    return False
                
            except Exception as e:
                logging.error(f"Failed to click play button: {str(e)}")
                return False
                
        except:
            logging.info("No play button found on cloudnestra page")
            return False
    except Exception as e:
        logging.error(f"Error checking cloudnestra play button: {str(e)}")
        return False

def find_cloudnestra_urls(driver):
    """Find all cloudnestra.com scriptlet URLs in the network traffic."""
    cloudnestra_urls = set()
    
    # Get initial network requests
    logs = driver.get_log('performance')
    for entry in logs:
        try:
            log = json.loads(entry['message'])['message']
            if 'Network.responseReceived' in log['method']:
                url = log['params']['response']['url']
                if 'cloudnestra.com' in url and '/rcp/' in url:
                    cloudnestra_urls.add(url)
                    logging.info(f"Found cloudnestra scriptlet URL: {url}")
        except:
            continue
    
    # Check for any iframes that might contain video players
    iframes = driver.find_elements(By.TAG_NAME, "iframe")
    for iframe in iframes:
        try:
            driver.switch_to.frame(iframe)
            time.sleep(2)
            
            # Get network requests from iframe
            iframe_logs = driver.get_log('performance')
            for entry in iframe_logs:
                try:
                    log = json.loads(entry['message'])['message']
                    if 'Network.responseReceived' in log['method']:
                        url = log['params']['response']['url']
                        if 'cloudnestra.com' in url and '/rcp/' in url:
                            cloudnestra_urls.add(url)
                            logging.info(f"Found cloudnestra scriptlet URL in iframe: {url}")
                except:
                    continue
            
            driver.switch_to.default_content()
        except:
            continue
    
    # Additional wait for any delayed requests
    time.sleep(5)
    
    # Get final network requests
    final_logs = driver.get_log('performance')
    for entry in final_logs:
        try:
            log = json.loads(entry['message'])['message']
            if 'Network.responseReceived' in log['method']:
                url = log['params']['response']['url']
                if 'cloudnestra.com' in url and '/rcp/' in url:
                    cloudnestra_urls.add(url)
                    logging.info(f"Found cloudnestra scriptlet URL: {url}")
        except:
            continue
    
    return list(cloudnestra_urls)

def check_url_headless(url):
    """Check if a video URL is valid by attempting to access it headlessly."""
    try:
        # Try to open the URL and get headers
        req = urllib.request.Request(url, method='HEAD')
        response = urllib.request.urlopen(req, timeout=10)
        
        # Check if we got a successful response
        if response.status == 200:
            # Get content type to verify it's a video
            content_type = response.headers.get('Content-Type', '').lower()
            if 'video' in content_type or 'stream' in content_type or 'application/x-mpegURL' in content_type:
                return True
        return False
    except (urllib.error.URLError, urllib.error.HTTPError, Exception) as e:
        logging.error(f"Error checking URL {url}: {str(e)}")
        return False

def save_working_url(url):
    """Save a working URL to headlessurl.txt"""
    try:
        with open('headlessurl.txt', 'a') as f:
            f.write(f"{url}\n")
        logging.info(f"Saved working URL to headlessurl.txt")
    except Exception as e:
        logging.error(f"Error saving URL to file: {str(e)}")

def try_play_in_vlc(video_links):
    """Try to play each link in VLC until one works."""
    if not video_links:
        logging.info("No video links to try")
        return False
        
    # Try each link in order
    for link in video_links:
        logging.info(f"Attempting to play: {link}")
        
        try:
            # Default VLC path for Windows
            vlc_path = r"C:\Program Files\VideoLAN\VLC\vlc.exe"
            
            # Check if VLC exists at default path
            if not os.path.exists(vlc_path):
                # Try alternative common paths
                alt_paths = [
                    r"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe",
                    os.path.expanduser("~\\AppData\\Local\\Programs\\VideoLAN\\VLC\\vlc.exe")
                ]
                for path in alt_paths:
                    if os.path.exists(path):
                        vlc_path = path
                        break
                else:
                    logging.error("VLC not found in common installation paths")
                    return False
            
            # Open VLC with the video URL
            process = subprocess.Popen([vlc_path, link])
            
            # Wait a bit for VLC to start
            time.sleep(5)
            
            # Check if process is still running
            if process.poll() is not None:
                logging.info("VLC process terminated unexpectedly")
                continue
            
            # Ask user if video is playing
            while True:
                response = input("\nIs the video playing correctly in VLC? (yes/no): ").lower().strip()
                if response in ['yes', 'no']:
                    break
                print("Please answer 'yes' or 'no'")
            
            if response == 'yes':
                logging.info("User confirmed video is playing correctly")
                return True
            else:
                logging.info("User reported video is not playing correctly, trying next link")
                # Kill the VLC process
                process.terminate()
                time.sleep(2)  # Wait for VLC to close
                continue  # Try next link
                    
        except Exception as e:
            logging.error(f"Error trying to play in VLC: {str(e)}")
            continue  # Try next link
    
    logging.info("Failed to play any of the links in VLC")
    return False

def try_play_in_ffplay(video_links, watch=False):
    """Try to play each link in FFplay until one works."""
    if not video_links:
        logging.info("No video links to try")
        return False
        
    # Try each link in order
    for link in video_links:
        if watch:
            logging.info(f"Attempting to play full video with FFplay: {link}")
        else:
            logging.info(f"Attempting to verify URL with FFplay: {link}")
        
        try:
            # Try to find ffplay in PATH
            ffplay_path = "ffplay"
            
            # Prepare FFplay arguments
            ffplay_args = [ffplay_path]
            if not watch:
                ffplay_args.extend(["-autoexit", "-t", "5"])
            ffplay_args.append(link)
            
            # Open FFplay with the video URL
            process = subprocess.Popen(ffplay_args)
            
            if watch:
                # If watching, wait for the process to complete
                process.wait()
                return True
            else:
                # If verifying, wait for FFplay to finish
                process.wait(timeout=10)
                # If we get here, the video played successfully
                logging.info(f"URL verified and working: {link}")
                return True
                    
        except subprocess.TimeoutExpired:
            # If FFplay is still running after timeout, it means the video is playing
            if not watch:
                process.terminate()
                logging.info(f"URL verified and working: {link}")
                return True
        except Exception as e:
            logging.error(f"Error verifying URL with FFplay: {str(e)}")
            continue  # Try next link
    
    logging.info("Failed to verify any of the URLs with FFplay")
    return False

def get_movie_details(driver):
    """Get movie description and genre from the page."""
    try:
        # Get description
        description = ""
        try:
            desc_element = driver.find_element(By.CSS_SELECTOR, "div.description")
            description = desc_element.text.strip()
        except:
            logging.info("Could not find movie description")

        # Get genre
        genre = ""
        try:
            genre_element = driver.find_element(By.CSS_SELECTOR, ".col-xl-7.col-lg-7.col-md-8.col-sm-12")
            genre = genre_element.text.strip()
        except:
            logging.info("Could not find movie genre")

        return description, genre
    except Exception as e:
        logging.error(f"Error getting movie details: {str(e)}")
        return "", ""

def check_play_icon(url, try_vlc=False, try_ffplay=False, watch_ffplay=False, headless=True):
    driver = None
    temp_dir = None
    
    try:
        # Set up Chrome options
        chrome_options = Options()
        if headless:
            chrome_options.add_argument('--headless=new')
        chrome_options.add_argument('--window-size=1920,1080')
        chrome_options.add_argument('--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36')
        chrome_options.add_argument('--disable-gpu')
        chrome_options.add_argument('--no-sandbox')
        chrome_options.add_argument('--disable-dev-shm-usage')
        chrome_options.add_argument('--disable-web-security')
        chrome_options.add_argument('--disable-features=IsolateOrigins,site-per-process')
        chrome_options.add_argument('--disable-site-isolation-trials')
        chrome_options.add_argument('--disable-blink-features=AutomationControlled')
        chrome_options.add_argument('--ignore-certificate-errors')
        chrome_options.add_argument('--allow-insecure-localhost')
        chrome_options.add_argument('--disable-webgl')
        chrome_options.add_argument('--disable-extensions')
        chrome_options.add_argument('--disable-popup-blocking')
        chrome_options.add_argument('--disable-notifications')
        chrome_options.add_argument('--disable-infobars')
        chrome_options.add_argument('--disable-logging')
        chrome_options.add_argument('--disable-default-apps')
        chrome_options.add_argument('--disable-translate')
        chrome_options.add_argument('--disable-sync')
        chrome_options.add_argument('--disable-background-networking')
        chrome_options.add_argument('--disable-background-timer-throttling')
        chrome_options.add_argument('--disable-backgrounding-occluded-windows')
        chrome_options.add_argument('--disable-breakpad')
        chrome_options.add_argument('--disable-component-extensions-with-background-pages')
        chrome_options.add_argument('--disable-dev-tools')
        chrome_options.add_experimental_option('excludeSwitches', ['enable-automation', 'enable-logging'])
        chrome_options.add_experimental_option('useAutomationExtension', False)
        
        # Enable performance logging
        chrome_options.set_capability('goog:loggingPrefs', {'performance': 'ALL'})
        
        # Create a temporary directory for Chrome user data
        temp_dir = tempfile.mkdtemp()
        chrome_options.add_argument(f'--user-data-dir={temp_dir}')

        # Initialize the Chrome WebDriver with service
        service = Service(ChromeDriverManager().install())
        driver = webdriver.Chrome(service=service, options=chrome_options)
        
        # Execute CDP commands to prevent detection
        driver.execute_cdp_cmd('Network.setUserAgentOverride', {
            "userAgent": 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36',
            "platform": "Windows",
            "acceptLanguage": "en-US,en;q=0.9"
        })
        
        # Additional anti-detection measures
        driver.execute_cdp_cmd('Page.addScriptToEvaluateOnNewDocument', {
            'source': '''
                Object.defineProperty(navigator, 'webdriver', {
                    get: () => undefined
                });
                Object.defineProperty(navigator, 'plugins', {
                    get: () => [1, 2, 3, 4, 5]
                });
                Object.defineProperty(navigator, 'languages', {
                    get: () => ['en-US', 'en']
                });
                window.chrome = {
                    runtime: {}
                };
            '''
        })
        
        # Enable network tracking
        driver.execute_cdp_cmd('Network.enable', {})
        
        # Navigate to the URL
        driver.get(url)
        
        # Get movie details
        description, genre = get_movie_details(driver)
        if description:
            logging.info("\nMovie Description:")
            logging.info(description)
        if genre:
            logging.info("\nMovie Genre:")
            logging.info(genre)
        
        # Wait for the page to load (wait up to 10 seconds)
        wait = WebDriverWait(driver, 10)
        
        # Try to find the play icon using different possible selectors
        selectors = [
            "i.fas.fa-play",
            "i[class*='fa-play']",
            "i[class*='play']"
        ]
        
        found = False
        play_element = None
        for selector in selectors:
            try:
                element = wait.until(EC.presence_of_element_located((By.CSS_SELECTOR, selector)))
                if element:
                    found = True
                    play_element = element
                    logging.info(f"Play icon found using selector: {selector}")
                    break
            except:
                continue
        
        if found and play_element:
            # Wait a bit to ensure the page is fully loaded
            time.sleep(2)
            
            # Try to click the element
            try:
                # Scroll the element into view
                driver.execute_script("arguments[0].scrollIntoView(true);", play_element)
                time.sleep(1)  # Wait for scroll to complete
                
                # Try different click methods
                try:
                    # Method 1: Direct click
                    play_element.click()
                    logging.info("Successfully clicked using direct click")
                except:
                    try:
                        # Method 2: JavaScript click
                        driver.execute_script("arguments[0].click();", play_element)
                        logging.info("Successfully clicked using JavaScript")
                    except:
                        # Method 3: Action Chains
                        ActionChains(driver).move_to_element(play_element).click().perform()
                        logging.info("Successfully clicked using Action Chains")
                
                logging.info("Successfully clicked the play icon!")
                
                # Monitor network traffic for cloudnestra URLs
                logging.info("Monitoring network traffic for cloudnestra URLs...")
                time.sleep(5)  # Wait for initial requests
                
                # Find cloudnestra URLs
                cloudnestra_urls = find_cloudnestra_urls(driver)
                
                if cloudnestra_urls:
                    logging.info("\nFound cloudnestra URLs:")
                    # Only take the first cloudnestra URL
                    first_url = cloudnestra_urls[0]
                    logging.info(f"Visiting first cloudnestra URL: {first_url}")
                    
                    # Check for play button and m3u8 URLs
                    has_play_button = check_cloudnestra_play_button(driver, first_url, headless)
                    if has_play_button and check_cloudnestra_play_button.found_m3u8:
                        logging.info("Successfully found m3u8 URLs!")
                        
                        # Store m3u8 URLs before closing browser
                        m3u8_urls = list(check_cloudnestra_play_button.m3u8_urls)
                        
                        # Close browser before starting video player
                        driver.quit()
                        try:
                            os.rmdir(temp_dir)
                        except:
                            pass
                        
                        # Try playing in VLC if requested
                        if try_vlc and m3u8_urls:
                            logging.info("\nAttempting to play video in VLC...")
                            try_play_in_vlc(m3u8_urls)
                        
                        # Try playing in FFplay if requested
                        if try_ffplay and m3u8_urls:
                            logging.info("\nAttempting to play video in FFplay...")
                            try_play_in_ffplay(m3u8_urls, watch_ffplay)
                        
                        return  # Exit after finding m3u8 URLs
                else:
                    logging.info("No cloudnestra URLs found in the network traffic")
                    
            except Exception as e:
                logging.error(f"Failed to click the play icon: {str(e)}")
        else:
            logging.info("Play icon not found on the page")
            
    except Exception as e:
        logging.error(f"An error occurred: {str(e)}")
    
    finally:
        if driver:
            try:
                # Close all windows and quit the driver
                driver.quit()
            except Exception as e:
                logging.debug(f"Error during browser cleanup: {str(e)}")
            finally:
                driver = None
                
        if temp_dir:
            try:
                shutil.rmtree(temp_dir, ignore_errors=True)
            except Exception as e:
                logging.debug(f"Error during temp directory cleanup: {str(e)}")
            finally:
                temp_dir = None

def process_movie_links(search_term=None, try_vlc=False, try_ffplay=False, watch_ffplay=False, headless=True, random_watch=False, roku_ip=None):
    """Process movie links from movie_links.txt file."""
    try:
        # Read movie_links.txt
        with open('movie_links.txt', 'r', encoding='utf-8') as f:
            movies = [line.strip() for line in f if line.strip()]

        # Filter movies based on search term if provided
        if search_term:
            search_term = search_term.lower()
            movies = [movie for movie in movies if search_term in movie.lower()]

        if not movies:
            logging.info("No movies found matching the search criteria")
            return

        if random_watch:
            # Select a random movie
            selected_movie = random.choice(movies)
            parts = selected_movie.split('|')
            if len(parts) >= 3:
                year = parts[0].strip()
                title = parts[1].strip()
                url = parts[2].strip()
                
                logging.info(f"\nRandomly selected movie: {year} | {title}")
                logging.info(f"URL: {url}")
                
                if roku_ip:
                    # Process the movie URL for Roku sideloading
                    check_play_icon(url, try_vlc=False, try_ffplay=False, watch_ffplay=False, headless=headless)
                    if check_cloudnestra_play_button.m3u8_urls:
                        video_url = list(check_cloudnestra_play_button.m3u8_urls)[0]
                        create_roku_app(title, video_url, roku_ip)
                else:
                    # Process the movie URL with VLC
                    check_play_icon(url, try_vlc=True, try_ffplay=False, watch_ffplay=False, headless=headless)
                return

        logging.info(f"Found {len(movies)} movies matching your search:")
        
        # Display numbered list of movies
        for i, movie in enumerate(movies, 1):
            parts = movie.split('|')
            if len(parts) >= 3:
                year = parts[0].strip()
                title = parts[1].strip()
                print(f"{i}. {year} | {title}")
        
        # Get user selection
        while True:
            try:
                selection = input("\nEnter the number of the movie to process (or 'q' to quit): ")
                if selection.lower() == 'q':
                    return
                
                index = int(selection) - 1
                if 0 <= index < len(movies):
                    selected_movie = movies[index]
                    break
                else:
                    print("Invalid selection. Please try again.")
            except ValueError:
                print("Please enter a valid number or 'q' to quit.")
        
        # Process the selected movie
        parts = selected_movie.split('|')
        if len(parts) >= 3:
            year = parts[0].strip()
            title = parts[1].strip()
            url = parts[2].strip()
            
            logging.info(f"\nProcessing: {year} | {title}")
            logging.info(f"URL: {url}")
            
            # Process the movie URL
            check_play_icon(url, try_vlc, try_ffplay, watch_ffplay, headless)

    except Exception as e:
        logging.error(f"Error reading movie_links.txt: {str(e)}")

def create_roku_app(title, video_url, roku_ip):
    """Create a Roku app with the given title and video URL."""
    try:
        # Create a new directory for the app
        app_dir = os.path.join("RokuSideload", title.replace(" ", "_"))
        if not os.path.exists(app_dir):
            os.makedirs(app_dir)
            
        # Copy all files from VideoPlay template
        template_dir = os.path.join("RokuSideload", "VideoPlay")
        for item in os.listdir(template_dir):
            s = os.path.join(template_dir, item)
            d = os.path.join(app_dir, item)
            if os.path.isdir(s):
                shutil.copytree(s, d, dirs_exist_ok=True)
            else:
                shutil.copy2(s, d)
        
        # Update videoscene.xml with title and URL
        videoscene_path = os.path.join(app_dir, "components", "videoscene.xml")
        with open(videoscene_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Update title and URL only, leave Video id unchanged
        content = content.replace('videocontent.title = ""', f'videocontent.title = "{title}"')
        content = content.replace('videocontent.url = ""', f'videocontent.url = "{video_url}"')
        
        with open(videoscene_path, 'w', encoding='utf-8') as f:
            f.write(content)
        
        # Create zip file
        zip_path = f"{app_dir}.zip"
        shutil.make_archive(app_dir, 'zip', app_dir)
        
        # Upload to Roku
        if upload_to_roku(roku_ip, zip_path):
            logging.info(f"Successfully uploaded {title} to Roku device at {roku_ip}")
            return True
        else:
            logging.error(f"Failed to upload {title} to Roku device")
            return False
            
    except Exception as e:
        logging.error(f"Error creating Roku app: {str(e)}")
        return False

def upload_to_roku(ip_address, file_path):
    """Upload a zip file to a Roku device."""
    try:
        if not os.path.exists(file_path):
            logging.error(f"File not found: {file_path}")
            return False

        chrome_options = Options()
        chrome_options.add_argument('--start-maximized')
        chrome_options.add_argument('--disable-gpu')
        chrome_options.add_argument('--no-sandbox')
        
        driver = webdriver.Chrome(service=Service(ChromeDriverManager().install()), options=chrome_options)
        wait = WebDriverWait(driver, 30)
        
        url = f'http://{ip_address}/'
        driver.get(url)
        
        logging.info("\nPlease log in to the Roku device in the browser window...")
        wait.until(EC.presence_of_element_located((By.TAG_NAME, "frameset")))
        
        frames = driver.find_elements(By.TAG_NAME, "frame")
        driver.switch_to.frame(frames[0])
        
        file_input = driver.find_element(By.CSS_SELECTOR, "input.form-input-file[name='archive']")
        file_input.send_keys(os.path.abspath(file_path))
        time.sleep(2)
        
        driver.execute_script("""
            var form = document.querySelector('form');
            if (form) {
                form.action = '/plugin_install';
                form.method = 'POST';
                form.enctype = 'multipart/form-data';
                form.submit();
            }
        """)
        
        time.sleep(15)
        driver.quit()
        return True
        
    except Exception as e:
        logging.error(f"Error uploading to Roku: {str(e)}")
        if 'driver' in locals():
            driver.quit()
        return False

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description='Extract m3u8 URLs from TinyZone movies')
    parser.add_argument('url', nargs='?', type=validate_tinyzone_url, help='URL of the movie from tinyzone.org (e.g., https://ww3.tinyzone.org/movie/example-123456/)')
    parser.add_argument('-VLC', '--vlc', action='store_true', help='Open the video links in VLC player')
    parser.add_argument('-FFPLAY', '--ffplay', action='store_true', help='Open the video links in FFplay')
    parser.add_argument('-w', '--w', action='store_true', help='Watch the full video in FFplay (use with -FFPLAY)')
    parser.add_argument('-Head', '--head', action='store_true', help='Run browser in visible mode (not headless)')
    parser.add_argument('-RW', '--rw', action='store_true', help='Select and play a random movie from the list using VLC')
    parser.add_argument('-S', '--search', help='Search for movies in movie_links.txt by title or year')
    parser.add_argument('-RokuSL', '--rokusl', help='Create and sideload a Roku app with the movie (requires Roku device IP address)')
    
    args = parser.parse_args()
    
    if args.search:
        process_movie_links(args.search, args.vlc, args.ffplay, args.w, not args.head, args.rw, args.rokusl)
    elif args.url:
        if args.rokusl:
            # Get movie details and video URL
            movie_details = None
            video_url = None
            
            # Process the URL to get video URL
            check_play_icon(args.url, args.vlc, args.ffplay, args.w, not args.head)
            
            # If we have a working video URL, create and sideload the Roku app
            if check_cloudnestra_play_button.m3u8_urls:
                video_url = list(check_cloudnestra_play_button.m3u8_urls)[0]
                # Extract title from URL
                title = args.url.split('/')[-2].replace('-', ' ').title()
                create_roku_app(title, video_url, args.rokusl)
        else:
            check_play_icon(args.url, args.vlc, args.ffplay, args.w, not args.head)
    else:
        process_movie_links(try_vlc=args.vlc, try_ffplay=args.ffplay, watch_ffplay=args.w, headless=not args.head, random_watch=args.rw, roku_ip=args.rokusl)
