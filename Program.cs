using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ProducerLabpt2
{
    class Program
    {
        static int finalDepth;
        static int numOfNetTasks = 0;
        static int numOfCpuTasks = 0;
        static int numOfThreadsOfEachType = 4;
        static Queue<Tuple<Uri, int, Uri>> netTasks = new Queue<Tuple<Uri, int, Uri>>();                //url, depth, url where we found the other url
        static Queue<Tuple<string, int, Uri>> CpuTasks = new Queue<Tuple<string, int, Uri>>();             //page content, depth, url correlating to the page content
        static object L = new object();
        static List<string> DeadLinks = new List<string>();
        static HashSet<string> AllLinks = new HashSet<string>();

        static void Main(string[] args)
        {
            Uri firstLink = new Uri(args[0]);
            finalDepth = Convert.ToInt32(args[1]);
            List<Thread> NetTaskThreads = new List<Thread>();
            List<Thread> CpuTaskThreads = new List<Thread>();
            netTasks.Enqueue(new Tuple<Uri, int, Uri>(firstLink, 0, null));


            for (int i = 0; i < numOfThreadsOfEachType; i++)
            {
                NetTaskThreads.Add(new Thread(() => { NetworkTask(); }));
                CpuTaskThreads.Add(new Thread(() => { CpuTask(); }));
                NetTaskThreads[i].Start();
                CpuTaskThreads[i].Start();
            }

            for (int i = 0; i < numOfThreadsOfEachType; i++)
            {
                NetTaskThreads[i].Join();
                CpuTaskThreads[i].Join();
            }

            //Printing The Dead Links
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("===================================================");
            Console.WriteLine("Dead Links:");

            int counter = 1;
            foreach (string link in DeadLinks)
            {
                Console.Write(counter);
                Console.Write(". ");
                Console.WriteLine(link);
                counter++;
            }
            Console.WriteLine("===================================================");
            Console.ReadLine();
        }

        static void PoisonPills()
        {
            //not locking because this should only ever be called while
            //lock is held
            netTasks.Enqueue(new Tuple<Uri, int, Uri>(null, -1, null));
            CpuTasks.Enqueue(new Tuple<string, int, Uri>(null, -1, null));
            return;
        }

        static void NetworkTask()
        {
            Tuple<Uri, int, Uri> myTask;
            myTask = new Tuple<Uri, int, Uri>(null, -1, null);
            WebClient w = new WebClient();
            bool meetsConditions = false;
            string result;

            lock (L)
            {
                Console.WriteLine("Network Thread started");
            }

            
            while (true)
            {
                meetsConditions = false;
                lock (L)
                {
                    if (netTasks.Count != 0) // is there any available tasks?
                    { 
                        myTask = netTasks.Dequeue();
                        if (myTask.Item2 == -1) // checking if you just dequeued a poison pill
                        {
                            PoisonPills();
                            Console.WriteLine("Network Thread Ended(recieved PP)");
                            Monitor.PulseAll(L);
                            return;
                        }
                        else
                        {
                            meetsConditions = true;
                            numOfNetTasks++;
                        }
                    }
                    else
                    {
                        if (numOfCpuTasks == 0 &&
                           numOfNetTasks == 0 &&
                           netTasks.Count == 0 &&
                           CpuTasks.Count == 0)
                        {
                            PoisonPills();
                            Console.WriteLine("Network Thread Ended2");
                            return;
                        }
                        else
                        {
                            Monitor.Wait(L);
                        }
                    }
                }

                if(meetsConditions && myTask.Item2 != -1)
                {
                    try
                    {
                        lock(L)
                        {
                            Console.Write("Attempting to Download: ");
                            Console.WriteLine(myTask.Item1.ToString());
                        }
                        result = w.DownloadString(myTask.Item1);
                        lock(L)
                        {
                            CpuTasks.Enqueue(new Tuple<string, int, Uri>(result, myTask.Item2, myTask.Item1));
                            Console.Write("Finished Downloading: ");
                            Console.WriteLine(myTask.Item1.ToString());
                            Monitor.PulseAll(L);
                        }
                    }
                    catch(Exception e)
                    {
                        lock (L)
                        {
                            Console.Write("Failed to Download: ");
                            Console.WriteLine(myTask.Item1.ToString());
                        }
                        DeadLinks.Add(myTask.Item1.ToString());
                    }
                    lock(L)
                    {
                        numOfNetTasks--;
                    }
                }
            }
        }

        static void CpuTask()
        {
            WebClient w = new WebClient();
            Tuple<string, int, Uri> myTask;
            myTask = new Tuple<string, int, Uri>(null, -1, null);
            bool meetsConditions = false;
            bool isLinkOK = false;
            Uri newLink = null;
            Regex rex = new Regex(@"<\s*a\s+.*href\s*=\s*[""']([^""']*)[""']");

            lock (L)
            {
                Console.WriteLine("CPU Thread started");
            }

            while (true)
            {
                meetsConditions = false;
                lock(L)
                {
                    if (CpuTasks.Count != 0)
                    {
                        
                        myTask = CpuTasks.Dequeue();
                        if(myTask.Item2 ==-1)
                        {
                            PoisonPills();
                            Console.WriteLine("Cpu Thread Ended1");
                            return;
                        }
                        else
                        {
                            meetsConditions = true;
                            numOfCpuTasks++;
                        }
                    }
                    else
                    {
                        if (numOfCpuTasks == 0 &&
                           numOfNetTasks == 0 &&
                           netTasks.Count == 0 &&
                           CpuTasks.Count == 0)
                        {
                            PoisonPills();
                            Console.WriteLine("Cpu Thread Ended2");
                            return;
                        }
                        else
                        {
                            Monitor.Wait(L);
                        }
                    }
                }

                if(meetsConditions && myTask.Item2 <= finalDepth)
                {
                    MatchCollection MC = rex.Matches(myTask.Item1);
                    foreach(Match M in MC)
                    {
                        isLinkOK = true;
                        try
                        {
                            newLink = new Uri(myTask.Item3, M.Groups[1].Value);
                        }
                        catch(Exception e)
                        {
                            isLinkOK = false;
                        }

                        if(isLinkOK)
                        {
                            lock(L)
                            {
                                if(!AllLinks.Contains(newLink.ToString()))
                                {
                                    AllLinks.Add(newLink.ToString());
                                    if(myTask.Item3.Host == newLink.Host)
                                    {
                                        netTasks.Enqueue(new Tuple<Uri, int, Uri>(newLink, myTask.Item2 + 1, myTask.Item3));
                                    }
                                }
                            }
                        }
                    }
                }

                lock (L)
                {
                    if (meetsConditions)
                    {
                        numOfCpuTasks--;
                        Monitor.PulseAll(L);
                    }
                }  
            }
        }
    }
}
