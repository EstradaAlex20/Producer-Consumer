﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net;
using System.Collections;

namespace OS2_Producer
{
    class Program
    {
        static HashSet<string> allLinks = new HashSet<string>();
        static HashSet<string> currentLinks = new HashSet<string>();
        static HashSet<string> deadLinks = new HashSet<string>();
        static BlockingCollection<int> bc = new BlockingCollection<int>()
        static Queue htmlQ = new Queue();
        static object L = new object();

        static void Main(string[] args)
        {

            //producer takes a url, checks if its dead, if not it 
            //outputs the html to the buffer

            //consumer takes html, and outputs all the links in the
            //html back to the buffer for the producers

            string url = args[0];
            int dist = Convert.ToInt32(args[1]);

            allLinks.Add(url);
            currentLinks.Add(url);

            //first round
            new Thread(() =>{ Producer(url); }).Start();


            List<Thread> T = new List<Thread>();
            T.Add(new Thread(() => { Producer(url); }));
            T[0].Start();
            foreach (var t in T)
            {
                t.Join();
            }

            
            for(int i = 0; i < dist; i++)
            {
                foreach(string link in links)
                {
                    T.Add(new Thread(() => { Producer(link); }));
                }

                foreach(Thread thred in T)
                {
                    thred.Start();
                }
            }
            

        }

        static void Producer(string url)
        {
            WebClient w = new WebClient();
            Uri address = new Uri(url);
            string result;
            try
            {
                result = w.DownloadString(address);
            }
            catch
            {
                lock (L)
                {
                    links.Remove(url);
                    deadLinks.Add(url + " is broken.");
                }
                return;
                
            }
            Regex rex = new Regex(@"<a\s+(?:[^>]*?\s+)?href=([\""'])(.*?)\1", RegexOptions.IgnoreCase);
            MatchCollection MC = rex.Matches(result);
            foreach(Match M in MC)
            {
                string s = M.Groups[2].Value;
                //int start = M.Groups[2].Index;
                lock (L)
                {
                    links.Add(s);
                }
            }
        }
    }
}




/*
 cpu 1
 R = new reg expr
 while(1){
    t = r2.Take()
    mc = R.Matches(t.Item1)
    foreach(var M in mc){
        string s = M.Groups[1].value;
        if(s.indexof("://")==-1){
            if(S.StartsWith("/")){
                var u=t.Item3;
                s = u.Protocol + "://" + u.Host + s;
            else{
                p=u.AbsolutePath;
                i = p.LastIndexof("/");
                p=p.substring(0,i);
                s = p + "/" + s;

    }

    }
                put the link in D2

    }



    }


    }
     
     
     
     
     
     
     
     
     
     
     
     
     
     
     
     */


