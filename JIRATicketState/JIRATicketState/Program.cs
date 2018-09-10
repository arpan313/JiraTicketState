using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Pathoschild.FluentJira;
using Pathoschild.FluentJira.Models;
using Pathoschild.Http.Client;
using RestSharp;
using RestSharp.Authenticators;

namespace JIRATicketState
{
    class Program
    {
        private static string _url = "";
        private static string _userName = "";
        private static string _password = "";
        static void Main(string[] args)
        {
          
            var resultData= new StringBuilder();

            if (args.Length == 0)
            {
                Console.WriteLine("Welcome.. Please follow below instruction to use me");
                Console.WriteLine("To get information about individual ticket pass -t {MEB-4952} fromState { like IN DEV } toState { like RESOLVED} example -t ticket Number 'IN DEV' 'RESOLVED' ");
                Console.WriteLine("To get information about all tickets in features -a {Epic Name} fromState { like IN DEV } toState { like RESOLVED} example -a 'Epic Name' 'IN DEV' 'RESOLVED' ");

            }
            else
            {
               
                if (args.Contains("-t"))
                {
                    if (args.Length == 4)
                    {
                        var resultStringTask = GetChangeLog(args[1], args[2], args[3]);
                        var continuation = resultStringTask.ContinueWith(x => SaveInFile(x.Result));
                        continuation.Wait();

                    }
                }
                else if (args.Contains("-a"))
                {
                    if (args.Length == 4)
                    {
                        var issueList= GetIssueFromEpic(args[1]);
                        foreach (var issueKey in issueList)
                        {
                            var resultStringTask = GetChangeLog(issueKey, args[2], args[3]);
                            var continuation = resultStringTask.ContinueWith(x => SaveInFile(x.Result));
                            continuation.Wait();
                        }

                    }
                }

                Console.WriteLine("Output is in result.txt under c drive and temp folder");
            }

            Console.ReadLine();
        }

        private static void SaveInFile(string result)
        {
            var filePath = @"C:\temp\result.txt";
            using (var writer = new StreamWriter(filePath, true))
            {
                writer.WriteLine(result);
            }
           
        }

        private static List<string> GetIssueFromEpic(string epicName)
        {
            var issueKeys= new List<string>();
            var restClient = new RestClient(_url)
            {
                Authenticator = new HttpBasicAuthenticator(_userName, _password)
            };
            var restRequest = new RestRequest("search?jql=cf[11600]='{epicName}'", Method.GET);
            restRequest.AddUrlSegment("epicName", epicName);
            var response= restClient.Execute(restRequest);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                JObject parsedArray = JObject.Parse(response.Content);
                var data= parsedArray["issues"];
                foreach (JObject parsedObject in data.Children<JObject>())
                {
                    foreach (JProperty parsedProperty in parsedObject.Properties())
                    {
                        string propertyName = parsedProperty.Name;
                        if (propertyName.Equals("key"))
                        {
                            string propertyValue = (string)parsedProperty.Value;
                            if (!issueKeys.Contains(propertyValue))
                            {
                                issueKeys.Add(propertyValue);
                            }
                        }
                    }
                }
            }
        
            return issueKeys;

        }
        private static async Task<string> GetChangeLog(string ticketNumber,string fromState, string toState)
        {
            var output= new StringBuilder();

            IClient client = new JiraClient(_url, _userName,
                _password);

            Issue issue = await client.GetAsync($"issue/{ticketNumber}")
                .WithArgument("expand", "changelog")
                .As<Issue>();
            DateTime? startTime = null;
            var endTime = DateTime.Now.AddDays(-365);
            var quickTransition = true;
            foreach (var historyItem in issue.ChangeLog.Histories.OrderBy(x=>x.Created))
            {
              
                foreach (var item in historyItem.Items)
                {
                    if (item.Field == "status")
                    {
                        if (item.ToString.ToLower() == fromState.ToLower())
                        {
                            var timeTransitioned = historyItem.Created;
                            quickTransition = false;
                            if (!startTime.HasValue)
                            {
                                startTime = timeTransitioned;
                            }
                        }
                        else if (item.ToString.ToLower() == toState.ToLower())
                        {
                            var timeTransitioned = historyItem.Created;
                            if (timeTransitioned.Ticks > endTime.Ticks)
                            {
                                endTime = timeTransitioned;
                            }

                        }

                    }
                  
                }
                
            }
            if (!quickTransition)
            {
                var time = endTime.Subtract(startTime.Value).Days;
                if (time >= 0)
                {
                    var resultString =
                        $"Issue: {issue.Key} = Transition time : {endTime.Subtract(startTime.Value).Days}";
                    Console.WriteLine(resultString);
                    output.AppendLine(resultString);
                }

            }
            return output.ToString();

        }
    }
}
