using NLog;
using System.Text;
using System.Net;
using System.Threading;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace HttpClientTest
{
    class Program
    {
        static readonly Logger logger = LogManager.GetCurrentClassLogger();

        const int _maxRequestsCount = 100;
        const int _retryCount = 5;
        const string _url = "http://localhost:60108/callback/abc";

        static long currentThreadCount = 0;
        static HttpClient httpClient;

        static void CallBackTask(object o)
        {
            try
            {
                var dateTimeStartRequest = DateTime.Now;

                try
                {
                    Task<HttpResponseMessage> response_task = httpClient.PostAsync(_url, new StringContent("Very important information", Encoding.UTF8, "application/x-www-form-urlencoded"));

                    if (response_task.Wait(300 * 1000))// 5 минут
                    {
                        HttpResponseMessage response = response_task.Result;
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            string content = response.Content.ReadAsStringAsync().Result;
                            content = content.Remove(0, 1);
                            content = content.Remove(content.Length - 1, 1);

                            var dateTimeEndRequest = DateTime.ParseExact(content, "dd.MM.yyyy HH:mm:ss:ffff", System.Globalization.CultureInfo.InvariantCulture);

                            TimeSpan requestDuration = dateTimeEndRequest - dateTimeStartRequest;
                            double totalMilliseconds = requestDuration.TotalMilliseconds;

                            logger.Log(LogLevel.Info, $"{requestDuration.Minutes}.{requestDuration.Seconds}.{requestDuration.Milliseconds}");
                        }
                    }
                    else
                    {
                        logger.Log(LogLevel.Error, $"Истекло время ожидания ответа от сервера");
                    }
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

            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => { return true; };
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.UseProxy = false;

            httpClient = new HttpClient(handler);

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