using NLog;
using System.Text;
using System.Net;
using System;

namespace HttpClientTest
{
    class Program
    {
        static readonly Logger logger = LogManager.GetCurrentClassLogger();

        const int _maxRequestsCount = 100;
        const int _retryCount = 5;
        const string _url = "http://localhost:60108/callback/abc";

        static long currentThreadCount = 0;

        static void CallBackTask(object o)
        {
            try
            {
                var dateTimeStartRequest = DateTime.Now;

                try
                {
                    string postData = "Very important information";
                    var data = new UTF8Encoding().GetBytes(postData);

                    var httpClient = WebRequest.Create(_url);

                    httpClient.ContentLength = data.Length;
                    httpClient.Method = "POST";
                    httpClient.ContentType = "application/x-www-form-urlencoded";

                    using (var stream = httpClient.GetRequestStream())
                    {
                        stream.Write(data, 0, data.Length);
                    }

                    string content;
                    using (var response = (HttpWebResponse)httpClient.GetResponse())
                    {
                        content = new StreamReader(response.GetResponseStream()).ReadToEnd();
                    }

                    content = content.Remove(0, 1);
                    content = content.Remove(content.Length - 1, 1);

                    var dateTimeEndRequest = DateTime.ParseExact(content, "dd.MM.yyyy HH:mm:ss:ffff", System.Globalization.CultureInfo.InvariantCulture);

                    TimeSpan requestDuration = dateTimeEndRequest - dateTimeStartRequest;
                    double totalMilliseconds = requestDuration.TotalMilliseconds;

                    logger.Log(LogLevel.Info, $"{requestDuration.Minutes}:{requestDuration.Seconds}:{requestDuration.Milliseconds:000}");
                }
                finally
                {
                    Interlocked.Decrement(ref currentThreadCount);
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, $"Ошибка отправки");
            }
        }

        static void Main()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 100;

            for (int i = 0; i < _retryCount; i++)
            {
                logger.Log(LogLevel.Info, string.Empty);

                logger.Log(LogLevel.Info, $"Итерация {i + 1}");

                for (int j = 0; j < _maxRequestsCount; j++)
                {
                    if (ThreadPool.QueueUserWorkItem(CallBackTask, null))
                    {
                        Interlocked.Increment(ref currentThreadCount);
                    }
                }

                while (true)
                {
                    if (currentThreadCount == 0)
                    {
                        break;
                    }

                    Thread.Sleep(50);
                }
            }

            Console.ReadLine();
        }
    }
}