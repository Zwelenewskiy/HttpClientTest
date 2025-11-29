using NLog;
using System.Text;
using System.Net;
using System.Threading;
using System;
using System.Net.Http;

namespace HttpClientTest
{
    class Program
    {
        static readonly Logger logger = LogManager.GetCurrentClassLogger();

        const int _maxRequestsCount = 100;
        const int _retryCount = 3;
        const string _url = "http://localhost:5262/WeatherForecast";

        static long currentThreadCount = 0;
        static HttpClient httpClient;

        static async void CallBackTask(object o)
        {
            try
            {
                DateTime dateTimeStartRequest = DateTime.Now;

                HttpResponseMessage response = await httpClient.PostAsync(_url, new StringContent("Very important information", Encoding.UTF8, "application/x-www-form-urlencoded"));

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();

                    var dateTimeEndRequest = DateTime.ParseExact(content, "dd.MM.yyyy HH:mm:ss:ffff", System.Globalization.CultureInfo.InvariantCulture);

                    TimeSpan requestDuration = dateTimeEndRequest - dateTimeStartRequest;
                    double totalMilliseconds = requestDuration.TotalMilliseconds;

                    logger.Log(LogLevel.Info, $"{requestDuration.Minutes}.{requestDuration.Seconds}.{requestDuration.Milliseconds}");
                }
                else
                {
                    logger.Log(LogLevel.Error, $"Ошибка отправки. StatusCode: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "Ошибка отправки. Message: " + Environment.NewLine + ex.Message);
            }
            finally
            {
                Interlocked.Decrement(ref currentThreadCount);
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
            httpClient.Timeout = new TimeSpan(days: 0, hours: 0, minutes: 5, seconds: 0, milliseconds: 0);

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