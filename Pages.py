from selenium import webdriver
from selenium.webdriver.chrome.service import Service
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC
from bs4 import BeautifulSoup
import time
import os
import argparse

def load_existing_urls():
    """Load existing URLs from the file to avoid duplicates"""
    existing_urls = set()
    if os.path.exists('movie_links.txt'):
        with open('movie_links.txt', 'r', encoding='utf-8') as f:
            for line in f:
                if '|' in line:
                    parts = line.split('|')
                    if len(parts) >= 3:
                        url = parts[2].strip()
                        existing_urls.add(url)
    return existing_urls

def get_movie_links(include_images=False):
    # Set up Chrome options
    chrome_options = Options()
    chrome_options.add_argument('--headless')
    chrome_options.add_argument('--disable-gpu')
    chrome_options.add_argument('--no-sandbox')
    chrome_options.add_argument('--disable-dev-shm-usage')
    
    # Load existing URLs
    existing_urls = load_existing_urls()
    print(f"Found {len(existing_urls)} existing entries")
    
    try:
        # Initialize the Chrome driver
        driver = webdriver.Chrome(options=chrome_options)
        
        # Create or append to file
        file_mode = 'a' if os.path.exists('movie_links.txt') else 'w'
        with open('movie_links.txt', file_mode, encoding='utf-8') as f:
            if file_mode == 'w':
                header = "Year | Title | URL"
                if include_images:
                    header += " | Image URL"
                f.write(header + "\n")
                f.write("-" * 50 + "\n")
            
            # Process all pages
            total_pages = 719
            for page in range(1, total_pages + 1):
                url = f"https://ww3.tinyzone.org/movie/{page}/"
                print(f"\nProcessing page {page}/{total_pages} ({url})")
                
                try:
                    # Load the page
                    driver.get(url)
                    
                    # Wait for the film_list-wrap to be present
                    wait = WebDriverWait(driver, 10)
                    wait.until(EC.presence_of_element_located((By.CLASS_NAME, "film_list-wrap")))
                    
                    # Give a little extra time for dynamic content
                    time.sleep(2)
                    
                    # Get the page source and parse with BeautifulSoup
                    soup = BeautifulSoup(driver.page_source, 'html.parser')
                    
                    # Find the film_list-wrap container
                    film_list = soup.find(class_="film_list-wrap")
                    
                    if film_list:
                        # Find all elements with class="flw-item" within the film_list-wrap
                        flw_items = film_list.find_all(class_="flw-item")
                        print(f"Found {len(flw_items)} movies on page {page}")
                        
                        new_movies = 0
                        # Process each movie
                        for item in flw_items:
                            try:
                                # Get the link
                                link = item.find('a')
                                if not link:
                                    continue
                                    
                                movie_url = link.get('href')
                                if not movie_url or movie_url in existing_urls:
                                    continue
                                    
                                # Get title
                                title = item.find('h3', class_='film-name')
                                title_text = title.text.strip() if title else "Unknown Title"
                                
                                # Get year from film-infor div
                                film_info = item.find('div', class_='film-infor')
                                year = "Unknown Year"
                                if film_info:
                                    year_span = film_info.find_all('span')[1] if len(film_info.find_all('span')) > 1 else None
                                    if year_span:
                                        year = year_span.text.strip()
                                
                                # Get image URL if requested
                                image_url = ""
                                if include_images:
                                    film_poster = item.find('div', class_='film-poster')
                                    if film_poster:
                                        img = film_poster.find('img')
                                        if img:
                                            image_url = img.get('data-src', img.get('src', ''))
                                
                                # Write to file
                                line = f"{year} | {title_text} | {movie_url}"
                                if include_images:
                                    line += f" | {image_url}"
                                f.write(line + "\n")
                                
                                existing_urls.add(movie_url)
                                new_movies += 1
                                print(f"Added: {title_text} ({year})")
                                
                            except Exception as e:
                                print(f"Error processing a movie: {e}")
                                continue
                        
                        print(f"Added {new_movies} new movies from page {page}")
                        
                    else:
                        print(f"Could not find the film_list-wrap container on page {page}")
                    
                except Exception as e:
                    print(f"Error processing page {page}: {e}")
                    continue
                
                # Save progress after each page
                f.flush()
            
            print(f"\nAll pages processed. Total unique movies: {len(existing_urls)}")
            
    except Exception as e:
        print(f"An error occurred: {e}")
    
    finally:
        # Always close the driver
        try:
            driver.quit()
        except:
            pass

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description='Scrape movie information from tinyzone.org')
    parser.add_argument('-img', '--include-images', action='store_true', help='Include image URLs in the output')
    args = parser.parse_args()
    
    get_movie_links(include_images=args.include_images) 