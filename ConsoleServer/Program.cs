using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;

namespace ConsoleServer
{
    class Http
    {
        static void Main()
        {
            using (HttpServer srvr = new HttpServer())
            {
                srvr.Start();
                Console.WriteLine("Press any key to quit.");
                Console.ReadLine();
            }
        }
    }

    class HttpServer : IDisposable
    {
        private readonly int _maxThreads;
        private readonly HttpListener _listener;
        private readonly Thread _listenerThread;
        private readonly ManualResetEvent _stop, _idle;
        private readonly Semaphore _busy;

        public HttpServer()
        {
            int maxThreads = Environment.ProcessorCount * 4;
            _maxThreads = maxThreads;
            _stop = new ManualResetEvent(false);
            _idle = new ManualResetEvent(false);
            _busy = new Semaphore(maxThreads, maxThreads);
            _listener = new HttpListener();
            _listenerThread = new Thread(HandleRequests);
        }

        public void Start()
        {
            _listener.Prefixes.Add("http://127.0.0.1:80/");
            _listener.Start();
            _listenerThread.Start();
        }

        public void Dispose()
        { Stop(); }

        public void Stop()
        {
            _stop.Set();
            _listenerThread.Join();
            _idle.Reset();

            _busy.WaitOne();
            if (_maxThreads != 1 + _busy.Release())
                _idle.WaitOne();

            _listener.Stop();
        }

        private void HandleRequests()
        {
            while (_listener.IsListening)
            {
                var context = _listener.BeginGetContext(ListenerCallback, null);
                if (0 == WaitHandle.WaitAny(new[] { _stop, context.AsyncWaitHandle }))
                    return;
            }
        }


        List<Pet> pets = new List<Pet>();

        private void ListenerCallback(IAsyncResult ar)
        {
            _busy.WaitOne();
            try
            {
                HttpListenerContext context;
                try
                { context = _listener.EndGetContext(ar); }
                catch (HttpListenerException)
                { return; }

                if (_stop.WaitOne(0, false))
                    return;
          
                context.Response.SendChunked = true;

                var TypeRequest = context.Request.HttpMethod;
                string Url = context.Request.RawUrl.ToString();

                switch (TypeRequest)
                {
                    case "GET":
                        if (Url == "/pets")
                        {
                            using (TextWriter tw = new StreamWriter(context.Response.OutputStream))
                            {
                                tw.WriteLine("<html><body><h1>");
                                string Text = "";
                                if (pets.Count > 0)
                                {
                                    foreach (Pet p in pets)
                                    {
                                        Text += p.Name + " " + p.Type + "<br>";
                                    }

                                }
                                else
                                {
                                    Text = "no pets in Petshop";
                                }
                                tw.WriteLine(Text);
                                tw.WriteLine("</h1></body></html>");
                            }
                        }
                        else
                        {
                            using (TextWriter tw = new StreamWriter(context.Response.OutputStream))
                            {
                                int Code = 400;
                                tw.WriteLine("<html><body><h1>");
                                string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
                                tw.WriteLine(CodeStr);
                                tw.WriteLine("</h1></body></html>");
                            }
                        }
                        break;
                    case "POST":
                        if (Url == "/pets")
                        {
                            var pet = new Dictionary<string, string>();
                            var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                            var requestBody = reader.ReadToEnd();

                            string[] nameValues = requestBody.Split('&');
                            foreach (var nameValue in nameValues)
                            {
                                string[] splitted = nameValue.Split('=');
                                pet.Add(splitted[0], splitted[1]);
                            }

                            try
                            {
                                pets.Add(new Pet() { Name = pet["Name"], Type = pet["Type"] });
                            }
                            catch {
                                using (TextWriter tw = new StreamWriter(context.Response.OutputStream))
                                {
                                    int Code = 400;
                                    tw.WriteLine("<html><body><h1>");
                                    string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
                                    tw.WriteLine(CodeStr);
                                    tw.WriteLine("</h1></body></html>");
                                }
                            }

                        }
                        else
                        {
                            using (TextWriter tw = new StreamWriter(context.Response.OutputStream))
                            {
                                int Code = 400;
                                tw.WriteLine("<html><body><h1>");
                                string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
                                tw.WriteLine(CodeStr);
                                tw.WriteLine("</h1></body></html>");
                            }
                        }
                        break;

                    default:
                        using (TextWriter tw = new StreamWriter(context.Response.OutputStream))
                        {
                            int Code = 400;
                            tw.WriteLine("<html><body><h1>");
                            string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
                            tw.WriteLine(CodeStr);
                            tw.WriteLine("</h1></body></html>");
                        }
                        break;
                }

            }

            finally
            {
                if (_maxThreads == 1 + _busy.Release())
                    _idle.Set();
            }
        }

    }        
}
