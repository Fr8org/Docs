﻿using System;
using System.Configuration;
using System.Linq;
using System.Threading;
using NUnit.Core;
using HealthMonitor.Configuration;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Data.SqlClient;

namespace HealthMonitor
{
    public class Program
    {
        static void Main(string[] args)
        {
            var sendEmailReport = false;
            var appName = "Unspecified App";
            var ensureTerminalsStartup = false;
            var selfHosting = false;
            var connectionStringArg = string.Empty;
            var specificTest = string.Empty;
            var appInsightsInstrumentationKey = string.Empty;
            int errorCount = 0;
            var overrideDbName = string.Empty;
            var connectionString = string.Empty;
            var csName = string.Empty;

            Debug.AutoFlush = true;

            if (args != null)
            {
                for (var i = 0; i < args.Length; ++i)
                {
                    if (args[i] == "--email-report")
                    {
                        sendEmailReport = true;
                    }
                    else if (args[i] == "--ensure-startup")
                    {
                        ensureTerminalsStartup = true;
                    }
                    else if (i > 0 && args[i - 1] == "--app-name" && args[i] != null)
                    {
                        appName = args[i];
                    }
                    else if (i > 0 && args[i - 1] == "--connectionString" && args[i] != null)
                    {
                        connectionStringArg = args[i];
                    }
                    else if (args[i] == "--self-hosting")
                    {
                        selfHosting = true;
                    }
                    else if (i > 0 && args[i - 1] == "--test" && args[i] != null)
                    {
                        specificTest = args[i];
                    }

                    // Application Insights instrumentation key. When specified, 
                    // test performance information will be posted to AI for website performance report. 
                    else if (i > 0 && args[i - 1] == "--aiik" && args[i] != null)
                    {
                        appInsightsInstrumentationKey = args[i];
                    }

                    // Overrides database name in the provided connection string. 
                    else if (i > 0 && args[i - 1] == "--overrideDbName" && args[i] != null)
                    {
                        overrideDbName = args[i];
                    }
                }

                if (!string.IsNullOrEmpty(overrideDbName) && string.IsNullOrEmpty(connectionStringArg))
                {
                    throw new ArgumentException("--overrideDbName can only be specified when --connectionString is specified.");
                }

                if (selfHosting)
                {
                    if (string.IsNullOrEmpty(connectionStringArg))
                    {
                        throw new ArgumentException("You should specify --connectionString \"{ConnectionStringName}={ConnectionString}\" argument when using self-hosted mode.");
                    }

                    var regex = new System.Text.RegularExpressions.Regex("([\\w\\d]{1,})=([\\s\\S]+)");
                    var match = regex.Match(connectionStringArg);
                    if (match == null || !match.Success || match.Groups.Count != 3)
                    {
                        throw new ArgumentException("Please specify connection string in the following format: \"{ConnectionStringName}={ConnectionString}\".");
                    }

                    connectionString = match.Groups[2].Value;
                    csName = match.Groups[1].Value;

                    if (!string.IsNullOrEmpty(overrideDbName))
                    {
                        // Override database name in the connection string
                        var builder = new SqlConnectionStringBuilder(connectionString);
                        builder.InitialCatalog = overrideDbName;
                        connectionString = builder.ToString();
                    }

                    UpdateConnectionString(csName, connectionString);
                }

            }

            var selfHostInitializer = new SelfHostInitializer();
            if (selfHosting)
            {
                selfHostInitializer.Initialize(csName + "=" + connectionString);
            }

            try
            {
                errorCount = new Program().Run(ensureTerminalsStartup, sendEmailReport, appName, specificTest, appInsightsInstrumentationKey);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (selfHosting)
                {
                    selfHostInitializer.Dispose();
                }
            }
            Environment.Exit(errorCount);
        }

        private static void UpdateConnectionString(string key, string value)
        {
            System.Configuration.Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            configuration.ConnectionStrings.ConnectionStrings[key].ConnectionString = value;
            configuration.Save();

            ConfigurationManager.RefreshSection("connectionStrings");
        }

        private void EnsureTerminalsStartUp()
        {
            var awaiter = new TerminalStartUpAwaiter();
            var failedToStart = awaiter.AwaitStartUp();

            if (failedToStart.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Following terminals have failed to start:");

                foreach (var terminalName in failedToStart)
                {
                    Console.WriteLine("{0}: {1}", terminalName, ConfigurationManager.AppSettings[terminalName]);
                }

                Environment.Exit(failedToStart.Count);
            }
        }

        private void ReportToConsole(string appName, TestReport report)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Application: {0}", appName);
            Console.WriteLine("Integration tests result: {0} / {1} passed", report.Tests.Count(x => x.Success), report.Tests.Count());
            Console.ForegroundColor = ConsoleColor.Gray;

            foreach (var test in report.Tests.Where(x => !x.Success))
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("----------------------------------------");

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Integration Test Failure: {0}", test.Name);

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("Message: {0}", test.Message);
                Console.WriteLine("StackTrace: {0}", test.StackTrace);
            }
        }

        private int Run(
            bool ensureTerminalsStartup,
            bool sendEmailReport,
            string appName,
            string test,
            string appInsightsInstrumentationKey)
        {
            CoreExtensions.Host.InitializeService();

            if (ensureTerminalsStartup)
            {
                EnsureTerminalsStartUp();
            }

            var testRunner = new NUnitTestRunner(appInsightsInstrumentationKey);
            var report = testRunner.Run(test);

            if (sendEmailReport)
            {
                if (report.Tests.Any(x => !x.Success))
                {
                    var reportBuilder = new HtmlReportBuilder();
                    var htmlReport = reportBuilder.BuildReport(appName, report);

                    var reportNotifier = new TestReportNotifier();
                    reportNotifier.Notify(appName, htmlReport);
                }
            }

            //ReportToConsole(appName, report); We now have real-time reporting
            return report.Tests.Count(x => !x.Success);
        }
    }
}
