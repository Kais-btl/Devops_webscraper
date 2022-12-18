using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace WebScraping
{
    public class Web
    {
        static int MAX_RESULTS = 5;
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            ChromeOptions options = new ChromeOptions();
            options.AddArgument("--disable-extensions");
            options.AddArgument("--disable-gpu");
            //options.AddArgument("--headless");
            options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/90.0.4430.85 Safari/537.36");
            IWebDriver driver = new ChromeDriver(options);


            Console.WriteLine("choose option: \n\t0 - stop\n\t1 - change max result count\n\n\t2 - Youtube\n\t3 - ICTJob\n\t4 - Trustpilot");
            string input = Console.ReadLine().ToLower();
            string prompt;
            while (input != "0")
            {
                if (input != "1")
                {
                    prompt = "Input a search term: ";
                }
                else
                {
                    prompt = "Enter the new maximum number of results: ";
                }
                Console.WriteLine(prompt);
                string query = Console.ReadLine().ToLower();
                switch (input)
                {
                    case "1":
                        MAX_RESULTS = int.Parse(Console.ReadLine().ToLower());
                        break;
                    case "2":
                        Console.WriteLine("Sorted by:\n\t0 - relevance\n\t1 - uploadDate (default)\n\t2 - viewCount\n\t3 - rating");
                        string sorting = Console.ReadLine().ToLower();
                        if (sorting != "") { 
                            Youtube(query, driver, sorting);
                        }
                        else
                        {
                            Youtube(query, driver);
                        }
                        break;
                    case "3":
                        IctJob(query, driver);
                        break;
                    case "4":
                        Console.WriteLine("Filter by rating: 1-5 stars (default all)\n\t| Input numbers with no separation:");
                        char[] stars = Console.ReadLine().ToLower().ToCharArray();
                        Console.WriteLine("Pick a language (default = en)");
                        string language = Console.ReadLine().ToLower();
                        if (language != "") { 
                            TrustPilot(query, driver, stars, language);
                        }
                        else
                        {
                            TrustPilot(query, driver, stars);
                        }
                        break;
                    default:
                        Console.WriteLine("Wrong input please try again");
                        break;
                }
                Console.WriteLine("choose option: \n\t0 - stop\n\t1 - change max result count\n\t2 - Youtube\n\t3 - ICTJob\n\t4 - Trustpilot");
                input = Console.ReadLine().ToLower();
            }
            Console.WriteLine("Done");
            driver.Quit();
        }
        static void Youtube(string query, IWebDriver driver, string sorting = "1")
        {
            Dictionary<string, string> sortingOptions = new Dictionary<string, string>()
                    {
                        { "0", "CAASAhAB" },
                        { "1", "CAISAhAB" },
                        { "2", "CAMSAhAB" },
                        { "3", "CAESAhAB" }
                    };

            string SCRAPE_URL = String.Format("https://www.youtube.com/results?search_query={0}&sp={1}", query, sortingOptions[sorting]);
            driver.Navigate().GoToUrl(SCRAPE_URL);

            var wait = new WebDriverWait(driver, new TimeSpan(0, 0, 8));
            wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete"));

            By consent = By.CssSelector("button[aria-label='Reject the use of cookies and other data for the purposes described']");
            var consentButton = driver.FindElements(consent).Count >= 1 ? driver.FindElement(consent) : null;
            if (consentButton != null)
            {
                consentButton.Click();
                Thread.Sleep(100);
            }

            waitUntil(wait, By.TagName("ytd-thumbnail-overlay-time-status-renderer"));

            ReadOnlyCollection<IWebElement> videos = driver.FindElements(By.TagName("ytd-video-renderer"));
            Console.WriteLine("Total number of visible videos for \"" + query + "\" are: " + videos.Count);

            /* Go through the Videos List and scrap the same to get the attributes of the videos in the channel */
            int count = 0;
            List<YouTubeVideo> videoItems = new List<YouTubeVideo>();
            foreach (IWebElement videoEl in videos)
            {
                if (videoEl.FindElement(By.Id("badges")).Text.Contains("LIVE"))
                {
                    Console.WriteLine("skipped Livestream");
                    continue;
                }
                if (count < MAX_RESULTS)
                {
                    YouTubeVideo video = new YouTubeVideo();
                    video.Title = videoEl.FindElement(By.Id("video-title")).Text;
                    video.Channel = videoEl.FindElement(By.CssSelector(".ytd-channel-name a")).GetAttribute("innerText");
                    video.Views = videoEl.FindElement(By.XPath(".//*[@id='metadata-line']/span[1]")).Text;
                    video.Duration = videoEl.FindElement(By.TagName("ytd-thumbnail-overlay-time-status-renderer")).Text;
                    if (video.Duration == "SHORTS")
                    {
                        string[] words = videoEl.FindElement(By.CssSelector("yt-formatted-string.ytd-video-renderer")).GetAttribute("aria-label").Split(" ");
                        video.Duration = "00:" + words[words.Length - 7].PadLeft(2, '0');
                    }
                    video.ReleaseDate = videoEl.FindElement(By.XPath(".//*[@id='metadata-line']/span[2]")).Text;
                    videoItems.Add(video);

                    Console.WriteLine("*******   Video: {0}   *******", count + 1);
                    Console.WriteLine("Video Title: " + video.Title);
                    Console.WriteLine("Video channel: " + video.Channel);
                    Console.WriteLine("Video Views: " + video.Views);
                    Console.WriteLine("Video Duration: " + video.Duration);
                    Console.WriteLine("Video Release Date: " + video.ReleaseDate);
                    Console.WriteLine("\n");
                }
                count++;
            }

            string fileName = "D:\\scraping\\Youtube.json";
            string jsonString = "";
            if (File.Exists(fileName))
            {
                JObject obj = JObject.Parse(File.ReadAllText(fileName));
                JArray newArray = JArray.Parse(JsonConvert.SerializeObject(videoItems));
                if (obj.ContainsKey(query))
                {
                    JArray oldArray = (JArray)obj[query];
                    oldArray.Merge(newArray);

                }
                else
                {
                    obj.Add(query, newArray);
                }
                jsonString = JsonConvert.SerializeObject(obj);
            }
            else
            {
                jsonString = JsonSerializer.Serialize(new Dictionary<string, List<YouTubeVideo>>
                {
                    { query, videoItems }
                });
            }
            File.WriteAllText(fileName, jsonString);
        }
        static void IctJob(string query, IWebDriver driver)
        {
            string SCRAPE_URL = String.Format("https://www.ictjob.lu/en/search-it-jobs?keywords={0}", query);
            driver.Navigate().GoToUrl(SCRAPE_URL);

            var wait = new WebDriverWait(driver, TimeSpan.FromMilliseconds(5000));
            wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete"));

            ReadOnlyCollection<IWebElement> jobs = driver.FindElements(By.CssSelector("li[itemtype='http://schema.org/JobPosting']"));
            Console.WriteLine("Total number of visible jobs for \"" + query + "\" are: " + jobs.Count);

            int count = 0;
            List<JobPost> jobItems = new List<JobPost>();
            foreach (IWebElement jobEl in jobs)
            {
                if (count < MAX_RESULTS)
                {
                    JobPost job = new JobPost();
                    job.Link = jobEl.FindElement(By.CssSelector("a[itemprop='title']")).GetAttribute("href");
                    job.Title = jobEl.FindElement(By.CssSelector("h2.job-title")).Text;
                    job.Company = jobEl.FindElement(By.CssSelector("span.job-company")).Text;
                    job.Date = jobEl.FindElement(By.CssSelector("span.job-date")).Text;
                    job.Location = jobEl.FindElement(By.CssSelector("span[itemprop='addressLocality']")).Text;
                    job.Keywords = jobEl.FindElement(By.CssSelector("span.job-keywords")).Text;
                    jobItems.Add(job);

                    Console.WriteLine("*******   Job: {0}   *******", count + 1);
                    Console.WriteLine("Job link: " + job.Link);
                    Console.WriteLine("Job title: " + job.Title);
                    Console.WriteLine("Job company: " + job.Company);
                    Console.WriteLine("Job date: " + job.Date);
                    Console.WriteLine("Job location: " + job.Location);
                    Console.WriteLine("Job keywords: " + job.Keywords);
                    Console.WriteLine("\n");
                }
                count++;
            }

            string fileName = "D:\\scraping\\Job.json";
            string jsonString = "";
            if (File.Exists(fileName))
            {
                JObject obj = JObject.Parse(File.ReadAllText(fileName));
                JArray newArray = JArray.Parse(JsonConvert.SerializeObject(jobItems));
                if (obj.ContainsKey(query))
                {
                    JArray oldArray = (JArray)obj[query];
                    oldArray.Merge(newArray);

                }
                else
                {
                    obj.Add(query, newArray);
                }
                jsonString = JsonConvert.SerializeObject(obj);
            }
            else
            {
                jsonString = JsonSerializer.Serialize(new Dictionary<string, List<JobPost>>
                {
                    { query, jobItems }
                });
            }
            File.WriteAllText(fileName, jsonString);

        }
        static void TrustPilot(string companyName, IWebDriver driver, char[] stars, string language = "en")
        {
            string SCRAPE_URL = String.Format("https://www.trustpilot.com/review/www.{0}?languages={1}", companyName, language);
            driver.Navigate().GoToUrl(SCRAPE_URL);

            var wait = new WebDriverWait(driver, new TimeSpan(0, 0, 8));
            wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete"));

            if (driver.FindElement(By.TagName("h1")).Text.Contains("Whoops!"))
            {
                Console.WriteLine("Company {0} not found, please check and try again", companyName);
                return;
            }

            var consentButton = driver.FindElements(By.Id("onetrust-reject-all-handler")).Count >= 1 ? driver.FindElement(By.Id("onetrust-reject-all-handler")) : null;
            if (consentButton != null)
            {
                consentButton.Click();
                Thread.Sleep(100);
            }

            Dictionary<char, string> starFilters = new Dictionary<char, string>()
                    {
                        { '1', "star-filter-page-filter-one" },
                        { '2', "star-filter-page-filter-two" },
                        { '3', "star-filter-page-filter-three" },
                        { '4', "star-filter-page-filter-four" },
                        { '5', "star-filter-page-filter-five" },
                    };
            foreach (char star in stars)
            {
                waitUntil(wait, By.Id(starFilters[star]));
                driver.FindElement(By.Id(starFilters[star])).Click();
                Thread.Sleep(500);
            }
            
            waitUntil(wait, By.CssSelector("article h2"));

            float averageRating = float.Parse(driver.FindElement(By.CssSelector("p[data-rating-typography]")).Text);
            String total = driver.FindElement(By.CssSelector("p[data-reviews-count-typography]")).Text.Split(" ")[0];
            int totalReviews = int.Parse(total, NumberStyles.AllowThousands);
            Dictionary<int, String> reviewsPerStar = new();
            ReadOnlyCollection<IWebElement> ratings = driver.FindElements(By.CssSelector("p[data-rating-distribution-row-percentage-typography]"));

            Console.WriteLine("----- Company: {0} ------", companyName);
            Console.WriteLine("Average rating: " + averageRating);
            Console.WriteLine("Total reviews: " + totalReviews);
            Console.WriteLine("Reviews per star: ");
            foreach (int index in Enumerable.Range(1, 5).Reverse())
            {
                reviewsPerStar.Add(index, ratings[ratings.Count - index].Text);
                Console.WriteLine("\t {0} stars: {1}", index, reviewsPerStar[index]);
            }
            Console.WriteLine("\n");

            Company company = new();
            company.Name = companyName;
            company.AverageRating = averageRating;
            company.TotalReviews = totalReviews;
            company.ReviewsPerStar = reviewsPerStar;

            ReadOnlyCollection<IWebElement> reviews = driver.FindElements(By.TagName("article"));
            Console.WriteLine("Total number of visible reviews for \"" + companyName + "\" are: " + reviews.Count);

            List<Review> reviewItems = new List<Review>();
            int count = 0;
            foreach (IWebElement reviewCard in reviews)
            {
                if (count < MAX_RESULTS)
                {
                    Review review = new Review();
                    review.Title = reviewCard.FindElement(By.TagName("h2")).Text;
                    review.User = reviewCard.FindElement(By.CssSelector("span[data-consumer-name-typography]")).Text;
                    review.Rating = reviewCard.FindElement(By.CssSelector("div[data-service-review-rating]")).GetAttribute("data-service-review-rating");
                    try { 
                    review.Text = reviewCard.FindElement(By.CssSelector("p[data-service-review-text-typography]")).Text;
                    }
                    catch (NoSuchElementException) {
                        review.Text = "";
                    }
                    review.Date = reviewCard.FindElement(By.CssSelector("p[data-service-review-date-of-experience-typography]")).Text.Split(":")[1];
                    review.Location = reviewCard.FindElement(By.CssSelector("div[data-consumer-country-typography] span")).Text;
                    reviewItems.Add(review);

                    Console.WriteLine("*******   Review: {0}   *******", count + 1);
                    Console.WriteLine("Title: " + review.Title);
                    Console.WriteLine("User: " + review.User);
                    Console.WriteLine("Rating: " + review.Rating + "/5");
                    Console.WriteLine("Text: " + review.Text);
                    Console.WriteLine("Date: " + review.Date);
                    Console.WriteLine("Location: " + review.Location);
                    Console.WriteLine("\n");
                }
                count++;
            }

            company.reviews = reviewItems;
            string fileName = "D:\\scraping\\Trustpilot.json";
            string jsonString = "";
            if (File.Exists(fileName))
            {
                JObject obj = JObject.Parse(File.ReadAllText(fileName));
                JArray newArray = JArray.Parse(JsonConvert.SerializeObject(reviewItems));
                if (obj.ContainsKey(company.Name))
                {
                    JArray oldArray = (JArray)obj[company.Name]["reviews"];
                    oldArray.Merge(newArray);
                }
                else
                {
                    obj.Add(company.Name, JToken.FromObject(company));
                }
                jsonString = JsonConvert.SerializeObject(obj);
            }
            else
            {
                jsonString = JsonSerializer.Serialize(new Dictionary<String, Company> { { company.Name, company } });
            }
            File.WriteAllText(fileName, jsonString);
        }
        private static void waitUntil(WebDriverWait wait,By selector)
        {
            wait.Until(d =>
            {
                try
                {
                    d.FindElement(selector);
                }
                catch (Exception ex)
                {
                    Type exType = ex.GetType();
                    if (exType == typeof(ElementNotVisibleException) ||
                        exType == typeof(NoSuchElementException) ||
                        exType == typeof(StaleElementReferenceException) ||
                        exType == typeof(ElementNotSelectableException))
                    {
                        return false; //By returning false, wait will still rerun the function.
                    }
                    else
                    {
                        throw; //Rethrow exception if it's not an ignored type.
                    }
                }
                return true;
            });
        }
        public class YouTubeVideo
        {
            public string Title { get; set; }
            public string Channel { get; set; }
            public string Views { get; set; }
            public string Duration { get; set; }
            public string ReleaseDate { get; set; }
        }

        public class JobPost
        {
            public string Link { get; set; }
            public string Company { get; set; }
            public string Title { get; set; }
            public string Keywords { get; set; }
            public string Date { get; set; }
            public string Location { get; set; }
        }

        public class Review
        {
            public string Title { get; set; }
            public string User { get; set; }
            public string Rating { get; set; }
            public string Date { get; set; }
            public string Location { get; set; }
            public string Text { get; set; }

        }

        public class Company
        {
            public string Name { get; set; }
            public float AverageRating { get; set; }
            public int TotalReviews { get; set; }
            public Dictionary<int, String> ReviewsPerStar { get; set; }
            public List<Review> reviews { get; set; }

        }
    }
}