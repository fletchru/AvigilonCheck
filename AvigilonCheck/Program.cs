using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using AvigilonDotNet;

namespace AvigilonCheck
{
    class Program
    {
        private static AvigilonSdk m_sdk;
        private static IAvigilonControlCenter m_controlCenter;
        private static IPEndPoint m_endPoint;
        private static IPAddress m_address;
        private static string m_userName = "";
        private static string m_password = "";
        private static int m_cameraCount = 0;

        private static void InitAvigilon()
        {
            // Create and initialize the control center SDK by passing the Avigilon
            // Control Center .NET SDK version the client application is expected to
            // run against.
            SdkInitParams initParams = new SdkInitParams(6, 2)
            {
                // Set to true to auto discover other Avigilon control center servers on the
                // network. If set to false, servers will have to be manually added via
                // AddNvr(IPEndPoint).
                AutoDiscoverNvrs = false,
                ServiceMode = true
            };

            // Create an instance of the AvigilonSdk class and call CreateInstance to
            // ensure application is compatible with SDK and no SDK components are missing.
            m_sdk = new AvigilonSdk();
            m_controlCenter = m_sdk.CreateInstance(initParams);
        }

        private static bool ParseCommandLine()
        {
            string[] commandLineArgs = Environment.GetCommandLineArgs();
            foreach (string arg in commandLineArgs)
            {
                if (arg.Length >= 2 && arg[0] == '-')
                {
                    string value = arg.Substring(2);
                    if (value.Length > 0)
                    {
                        if (arg[1] == 's')
                        {
                            if (IPAddress.TryParse(value, out IPAddress address))
                            {
                                m_address = address;
                            }
                        }
                        else if (arg[1] == 'u')
                        {
                            m_userName = value;
                        }
                        else if (arg[1] == 'p')
                        {
                            m_password = value;
                        }
                        else if (arg[1] == 'c')
                        {
                            if (Int16.TryParse(value, out Int16 cameraCount))
                            {
                                m_cameraCount = cameraCount;
                            }
                        }
                    }
                }
            }
            if (m_address == null)
            {
                return false;
            }

            return true;
        }

        static void Main(string[] args)
        {
            if (ParseCommandLine())
            {
                InitAvigilon();

                m_endPoint = new IPEndPoint(m_address, m_controlCenter.DefaultNvrPortNumber);
                AvgError result = m_controlCenter.AddNvr(m_endPoint);

                if (ErrorHelper.IsError(result))
                {
                    Console.WriteLine("An error occurred while adding the NVR." + m_endPoint.Address);
                }

                DateTime waitEnd = DateTime.Now + new TimeSpan(0, 0, 10);
                INvr nvr = null;
                while (DateTime.Now < waitEnd && nvr == null)
                {
                    nvr = m_controlCenter.GetNvr(m_endPoint.Address);
                    if (nvr == null)
                    {
                        Thread.Sleep(500);
                    }
                }

                if (nvr == null)
                {
                    Console.WriteLine("An error occurred while connecting to the NVR.");
                }
                else
                {
                    LoginResult loginResult = nvr.Login(m_userName, m_password);
                    if (loginResult != 0)
                    {
                        Console.WriteLine("Failed to login to NVR: " + loginResult);
                    }
                    else
                    {
                        waitEnd = DateTime.Now + new TimeSpan(0, 0, 10);
                        while (DateTime.Now < waitEnd)
                        {
                            List<IDevice> devices = nvr.Devices;
                            
                            if (devices.Count == m_cameraCount)
                            {
                                string fileName = m_address.ToString().Replace(".", "") + ".xml";
                                string filePath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + "/" + fileName;

                                if (!File.Exists(filePath))
                                {
                                    XDocument doc = new XDocument();
                                    
                                    XElement root = new XElement("root");
                                    doc.Add(root);

                                    foreach (IDevice device in devices)
                                    {
                                        root.Add(new XElement("id" + device.Entities.FirstOrDefault().LogicalId, device.Connected.ToString()));
                                    }
                                    XmlWriterSettings xws = new XmlWriterSettings { OmitXmlDeclaration = true };
                                    using (XmlWriter xw = XmlWriter.Create(filePath, xws))
                                    {
                                        doc.Save(xw);
                                    }
                                }
                                break;
                            }

                            Thread.Sleep(500);
                        }
                    }
                }

                m_controlCenter?.Dispose();
                m_sdk.Shutdown();
            }
        }
    }
}
