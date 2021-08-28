using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using System.Text.RegularExpressions;

namespace bizIncentive
{
    class Program
    {
        const string chanCdnThread = @"https://a.4cdn.org/{0}/thread/{1}.json";
        const string chanThreadUrl = @"https://boards.4channel.org/{0}/thread/{1}";

        static async Task Main(string[] args)
        {
            Console.WriteLine("Enter search term");
            string searchTerm = Console.ReadLine();

            while (searchTerm == string.Empty)
            {
                Console.WriteLine("Enter a valid search term");
                searchTerm = Console.ReadLine();
            }

            Console.WriteLine("Board you wish to scan (default=biz)");

            var board = Console.ReadLine();
            const string bizBoard = "biz";
            board = board == string.Empty ? bizBoard : board;


            Console.WriteLine("Skip first n threads? (default=2)");
            var skipThreadsCountStr = Console.ReadLine();
            int skipThreadsCount = 2;
            if (!string.IsNullOrEmpty(skipThreadsCountStr))
            {
                skipThreadsCount = int.Parse(skipThreadsCountStr);
            }

            string chanCdnCatalogs = @$"https://a.4cdn.org/{board}/catalog.json";

            var client = new HttpClient();
            var res = await client.GetStringAsync(chanCdnCatalogs);

            var catalogRes = JsonSerializer.Deserialize<CatalogRes[]>(res);
            var threads = catalogRes.SelectMany(catalog => catalog.threads).ToArray();
            var threadUrls = threads.Select(thread => string.Format(chanCdnThread, board, thread.no));

            // start 4chanThread calls without batching
            var threadsTasks = threadUrls.Skip(skipThreadsCount).Select(t => client.GetStringAsync(t)).ToArray(); // test this out

            PrintDivider();
            Console.WriteLine($"Matches for {searchTerm}");

            PrintDivider();
            var matchingThreads = threads.Where(t => IContains(t.sub, searchTerm) || IContains(t.com, searchTerm)).ToArray();
            Console.WriteLine($"Op matches count: {matchingThreads.Length}");
            foreach (var thread in matchingThreads)
            {
                Console.WriteLine(string.Format(chanThreadUrl, board, thread.no) + " - op match");
            }

            PrintDivider();
            Console.WriteLine("Matches in threads");
            var threadsRes = await Task.WhenAll(threadsTasks);
            
            var threadsParsed = threadsRes.Select(thread => JsonSerializer.Deserialize<Thread>(thread)).ToArray();
            foreach (var thread in threadsParsed)
            {
                var matchingComments = thread.posts.Skip(1).Count(p => IContains(CleanComment(p.com), searchTerm)); // skip op's post as it is already checked
                if (matchingComments > 0)
                {
                    var threadNo = thread.posts.FirstOrDefault().no;
                    Console.WriteLine(string.Format(chanThreadUrl, board, threadNo) + $" - {matchingComments} matches");
                }
            }

            Console.WriteLine("Press any key to close");
            Console.ReadKey();
        }

        private static string CleanComment(string comment)
        {
            if (comment == null)
            {
                return comment;
            }

            var aIndex = comment.IndexOf("a>");
            var shouldClean = aIndex > -1;

            if (!shouldClean)
            {
                return comment;
            }

            var cleanComment = comment.Substring(aIndex + 2);
            return cleanComment.Replace("<br>", " ");
        }
        
        private static bool IContains(string text, string phrase)
        {
            if(text == null)
            {
                return false;
            }

            string pattern = @$"\b{phrase}\b";
            Match m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            return m.Success;
        }

        private static void PrintDivider()
        {
            Console.WriteLine("————————————————————————————————————");
        }
    }

    class CatalogRes
    {
        public CatalogThread[] threads { get; set; }
    }

    class CatalogThread
    {
        public int no { get; set; } //id
        public string sub { get; set; } //name of the thread
        public string com { get; set; } //op's comment

    }

    class Thread
    {
        public ThreadPost[] posts { get; set; }

    }

    class ThreadPost
    {
        public int no { get; set; }
        public string com { get; set; }
    }
}