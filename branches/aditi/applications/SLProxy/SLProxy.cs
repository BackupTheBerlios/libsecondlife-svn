/*
 * SLProxy.cs: implementation of Second Life proxy library
 *
 * Copyright (c) 2006 Austin Jennings
 * Pregen modifications made by Andrew Ortman on Dec 10, 2006 -> Dec 20, 2006
 * 
 * 
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without 
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the Second Life Reverse Engineering Team nor the names 
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 * 
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" 
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE 
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE 
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE 
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF 
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN 
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE 
 * POSSIBILITY OF SUCH DAMAGE.
 */

// #define DEBUG_SEQUENCE

using Nwc.XmlRpc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using libsecondlife;
using libsecondlife.Packets;

// SLProxy: proxy library for Second Life
namespace SLProxy {
	// ProxyConfig: configuration for proxy objects
	public class ProxyConfig {
		// userAgent: name of the proxy application
		public string userAgent;
		// author: email address of the proxy application's author
		public string author;
		// loginPort: port that the login proxy will listen on
		public ushort loginPort = 8080;
		// clientFacingAddress: address from which to communicate with the client
		public IPAddress clientFacingAddress = IPAddress.Loopback;
		// remoteFacingAddress: address from which to communicate with the server
		public IPAddress remoteFacingAddress = IPAddress.Any;
		// remoteLoginUri: URI for Second Life's login server
		public Uri remoteLoginUri = new Uri("https://login.agni.lindenlab.com/cgi-bin/login.cgi");
		// verbose: whether or not to print informative messages
		public bool verbose = true;

		// ProxyConfig: construct a default proxy configuration with the specified userAgent, author, and protocol
		public ProxyConfig(string userAgent, string author) {
			this.userAgent = userAgent;
			this.author = author;
		}

		// ProxyConfig: construct a default proxy configuration, parsing command line arguments (try --proxy-help)
		public ProxyConfig(string userAgent, string author, string[] args) : this(userAgent, author) {
			Hashtable argumentParsers = new Hashtable();
			argumentParsers["proxy-help"] = new ArgumentParser(ParseHelp);
			argumentParsers["proxy-login-port"] = new ArgumentParser(ParseLoginPort);
			argumentParsers["proxy-client-facing-address"] = new ArgumentParser(ParseClientFacingAddress);
			argumentParsers["proxy-remote-facing-address"] = new ArgumentParser(ParseRemoteFacingAddress);
			argumentParsers["proxy-remote-login-uri"] = new ArgumentParser(ParseRemoteLoginUri);
			argumentParsers["proxy-verbose"] = new ArgumentParser(ParseVerbose);
			argumentParsers["proxy-quiet"] = new ArgumentParser(ParseQuiet);

			foreach (string arg in args)
				foreach (string argument in argumentParsers.Keys) {
					Match match = (new Regex("^--" + argument + "(?:=(.*))?$")).Match(arg);
					if (match.Success) {
						string value;
						if (match.Groups[1].Captures.Count == 1)
							value = match.Groups[1].Captures[0].ToString();
						else
							value = null;
						try {
							((ArgumentParser)argumentParsers[argument])(value);
						} catch {
							Console.WriteLine("invalid value for --" + argument);
							ParseHelp(null);
						}
					}
				}
		}

		private delegate void ArgumentParser(string value);

		private void ParseHelp(string value) {
			Console.WriteLine("Proxy command-line arguments:"                                                   );
			Console.WriteLine("  --proxy-help                        display this help"                         );
			Console.WriteLine("  --proxy-login-port=<port>           listen for logins on <port>"               );
			Console.WriteLine("  --proxy-client-facing-address=<IP>  communicate with client via <IP>"          );
			Console.WriteLine("  --proxy-remote-facing-address=<IP>  communicate with server via <IP>"          );
			Console.WriteLine("  --proxy-remote-login-uri=<URI>      use SL login server at <URI>"              );
			Console.WriteLine("  --proxy-verbose                     display proxy notifications"               );
			Console.WriteLine("  --proxy-quiet                       suppress proxy notifications"              );

			Environment.Exit(1);
		}

		private void ParseLoginPort(string value) {
			loginPort = Convert.ToUInt16(value);
		}

		private void ParseClientFacingAddress(string value) {
			clientFacingAddress = IPAddress.Parse(value);
		}

		private void ParseRemoteFacingAddress(string value) {
			remoteFacingAddress = IPAddress.Parse(value);
		}

		private void ParseRemoteLoginUri(string value) {
			remoteLoginUri = new Uri(value);
		}

		private void ParseVerbose(string value) {
			if (value != null)
				throw new Exception();

			verbose = true;
		}

		private void ParseQuiet(string value) {
			if (value != null)
				throw new Exception();

			verbose = false;
		}
	}

	// Proxy: Second Life proxy server
	// A Proxy instance is only prepared to deal with one client at a time.
	public class Proxy {
		private ProxyConfig proxyConfig;

		/*
		 * Proxy Management
		 */

		// Proxy: construct a proxy server with the given configuration
		public Proxy(ProxyConfig proxyConfig) {
			this.proxyConfig = proxyConfig;

			InitializeLoginProxy();
			InitializeSimProxy();
		}

		object keepAliveLock = new Object();

		// Start: begin accepting clients
		public void Start() { lock(this) {
			System.Threading.Monitor.Enter(keepAliveLock);
			(new Thread(new ThreadStart(KeepAlive))).Start();

			RunSimProxy();
			Thread runLoginProxy = new Thread(new ThreadStart(RunLoginProxy));
			runLoginProxy.IsBackground = true;
			runLoginProxy.Start();

			IPEndPoint endPoint = (IPEndPoint)loginServer.LocalEndPoint;
			IPAddress displayAddress;
			if (endPoint.Address == IPAddress.Any)
				displayAddress = IPAddress.Loopback;
			else
				displayAddress = endPoint.Address;
			Log("proxy ready at http://" + displayAddress + ":" + endPoint.Port + "/", false);
		}}

		// Stop: allow foreground threads to die
		public void Stop() { lock(this) {
			System.Threading.Monitor.Exit(keepAliveLock);
		}}

		// KeepAlive: blocks until the proxy is free to shut down
		public void KeepAlive() {
            lock (keepAliveLock) { };
		}

		// SetLoginRequestDelegate: specify a callback loginRequestDelegate that will be called when the client requests login
		public void SetLoginRequestDelegate(XmlRpcRequestDelegate loginRequestDelegate) { lock(this) {
			this.loginRequestDelegate = loginRequestDelegate;
		}}

		// SetLoginResponseDelegate: specify a callback loginResponseDelegate that will be called when the server responds to login
		public void SetLoginResponseDelegate(XmlRpcResponseDelegate loginResponseDelegate) { lock(this) {
			this.loginResponseDelegate = loginResponseDelegate;
		}}

		// AddDelegate: add callback packetDelegate for packets of type packetName going direction
		public void AddDelegate(PacketType packetType, Direction direction, PacketDelegate packetDelegate) { lock(this) {
			Dictionary<PacketType, List<PacketDelegate>> delegates = (direction == Direction.Incoming ? incomingDelegates : outgoingDelegates);
			if (!delegates.ContainsKey(packetType)) {
				delegates[packetType] = new List<PacketDelegate>();
			}
			List<PacketDelegate> delegateArray = delegates[packetType];
			if(!delegateArray.Contains(packetDelegate)) {
				delegateArray.Add(packetDelegate);
			}
		}}

		// RemoveDelegate: remove callback for packets of type packetName going direction
		public void RemoveDelegate(PacketType packetType, Direction direction, PacketDelegate packetDelegate) { lock(this) {
			Dictionary<PacketType, List<PacketDelegate>> delegates = (direction == Direction.Incoming ? incomingDelegates : outgoingDelegates);
			if (!delegates.ContainsKey(packetType)) {
				return;
			}
			List<PacketDelegate> delegateArray = delegates[packetType];
			if(delegateArray.Contains(packetDelegate)) {
				delegateArray.Remove(packetDelegate);
			}
		}}

		private Packet callDelegates(Dictionary<PacketType, List<PacketDelegate>> delegates, Packet packet, IPEndPoint remoteEndPoint) {
			PacketType origType = packet.Type;
			foreach (PacketDelegate del in delegates[origType]) {
				packet = del(packet, remoteEndPoint);

				// FIXME: how should we handle the packet type changing?
				if(packet == null || packet.Type != origType) break;
			}
			return packet;
		}

		// InjectPacket: send packet to the client or server when direction is Incoming or Outgoing, respectively
		public void InjectPacket(Packet packet, Direction direction) { lock(this) {
			if (activeCircuit == null) {
				// no active circuit; queue the packet for injection once we have one
				ArrayList queue = direction == Direction.Incoming ? queuedIncomingInjections : queuedOutgoingInjections;
				queue.Add(packet);
			} else
				// tell the active sim proxy to inject the packet
				((SimProxy)simProxies[activeCircuit]).Inject(packet, direction);
		}}

		// Log: write message to the console if in verbose mode
		private void Log(object message, bool important) {
			if (proxyConfig.verbose || important)
				Console.WriteLine(message);
		}

		/*
		 * Login Proxy
		 */

		private Socket loginServer;
		private int capsReqCount = 0;

		// InitializeLoginProxy: initialize the login proxy
		private void InitializeLoginProxy() {
			loginServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			loginServer.Bind(new IPEndPoint(proxyConfig.clientFacingAddress, proxyConfig.loginPort));
			loginServer.Listen(1);
		}

		// RunLoginProxy: process login requests from clients
		private void RunLoginProxy() {
		    try {
			for (;;) {
				Socket client = loginServer.Accept();
				IPEndPoint clientEndPoint = (IPEndPoint)client.RemoteEndPoint;

				Log("handling HTTP request from " + clientEndPoint, false);


				try {
					Thread connThread = new Thread((ThreadStart)delegate { ProxyHTTP(client); });
					connThread.Start();
				} catch (Exception e) {
					Log("login failed: " + e.Message, false);
				}


				// send any packets queued for injection
				if (activeCircuit != null) lock(this) {
					SimProxy activeProxy = (SimProxy)simProxies[activeCircuit];
					foreach (Packet packet in queuedOutgoingInjections)
						activeProxy.Inject(packet, Direction.Outgoing);
					queuedOutgoingInjections = new ArrayList();
				}
			}
		    } catch (Exception e) {
			Console.WriteLine(e.Message);
			Console.WriteLine(e.StackTrace);
		    }
		}

		private class HandyNetReader {
			private NetworkStream netStream;
			private const int BUF_SIZE = 8192;
			private byte[] buf = new byte[BUF_SIZE];
			private int bufFill = 0;
	
			public HandyNetReader(NetworkStream s) {
				netStream = s;
			}

			public byte[] ReadLine() {
				int i = -1;
				while(true) {
					i = Array.IndexOf(buf,(byte)'\n',0,bufFill);
					if(i >= 0) break;
					if(bufFill >= BUF_SIZE) return null;
					if(!ReadMore()) return null;
				}
				byte[] ret = new byte[i];
				Array.Copy(buf,ret,i);
				Array.Copy(buf,i+1,buf,0,bufFill-(i+1));
				bufFill -= i+1;
				return ret;
			}

			private bool ReadMore() {
				int n = netStream.Read(buf,bufFill,BUF_SIZE-bufFill);
				bufFill += n;
				return n > 0;
			}

			public int Read(byte[] rbuf, int start, int len) {
				int read = 0;
				while(len > bufFill) {
					Array.Copy(buf,0,rbuf,start,bufFill);
					start += bufFill; len -= bufFill;
					read += bufFill; bufFill = 0;
					if(!ReadMore()) break;
				}
				Array.Copy(buf,0,rbuf,start,len);
				Array.Copy(buf,len,buf,0,bufFill-len);
				bufFill -= len; read += len;
				return read;
			}
		}

		// ProxyHTTP: proxy a HTTP request
		private void ProxyHTTP(Socket client) {
			NetworkStream netStream = new NetworkStream(client);
			HandyNetReader reader = new HandyNetReader(netStream);

			string line; int reqNo;
			int contentLength = 0;
			Match match;
			string uri; string meth;
			Dictionary<string,string> headers = new Dictionary<string,string>();

			lock(this) {
				capsReqCount++; reqNo = capsReqCount;
			}

			line = Encoding.UTF8.GetString(reader.ReadLine()).Replace("\r","");

			if (line == null)
				throw new Exception("EOF in client HTTP header");

			match = new Regex(@"^(\S+)\s+(\S+)\s+(HTTP/\d\.\d)$").Match(line);

			if(!match.Success) {
				Console.WriteLine("["+reqNo+"] Bad request!");
				byte[] wr = Encoding.ASCII.GetBytes("HTTP/1.0 400 Bad Request\r\nContent-Length: 0\r\n\r\n");
            			netStream.Write(wr,0,wr.Length);
				netStream.Close();  client.Close();
				return;
			}
		

			meth = match.Groups[1].Captures[0].ToString();
			uri = match.Groups[2].Captures[0].ToString();
			Console.WriteLine("["+reqNo+"] "+meth+": "+uri);

			// read HTTP header
			do {
				// read one line of the header
				line =  Encoding.UTF8.GetString(reader.ReadLine()).Replace("\r","");

				// check for premature EOF
				if (line == null)
					throw new Exception("EOF in client HTTP header");

				if(line == "") break;

				match = new Regex(@"^([^:]+):\s*(.*)$").Match(line);
		
				if(!match.Success) {
					Console.WriteLine("["+reqNo+"] Bad header: '"+line+"'");
					byte[] wr = Encoding.ASCII.GetBytes("HTTP/1.0 400 Bad Request\r\nContent-Length: 0\r\n\r\n");
            				netStream.Write(wr,0,wr.Length);
					netStream.Close();  client.Close();
					return;
				}
				
				string key = match.Groups[1].Captures[0].ToString();
				string val = match.Groups[2].Captures[0].ToString();
				headers[key.ToLower()] = val;

				Console.WriteLine("["+reqNo+"] Set '"+key.ToLower()+"' = '"+val+"'");
					
			} while (line != "");

			if(headers.ContainsKey("content-length")) {
				contentLength = Convert.ToInt32(headers["content-length"]);
				
			}

			// read the HTTP body into a buffer
			byte[] content = new byte[contentLength];
			reader.Read(content, 0, contentLength);

			Console.WriteLine("["+reqNo+"] request length = "+contentLength+":\n"+Encoding.UTF8.GetString(content)+"\n-------------");

			if(uri == "/") {
				ProxyLogin(netStream,content);
			} else if(new Regex(@"^/https?://.*$").Match(uri).Success) {
				ProxyCaps(netStream,meth,uri.Substring(1),headers,content,reqNo);
			} else {
				Console.WriteLine("404 not found: "+uri);
				byte[] wr = Encoding.ASCII.GetBytes("HTTP/1.0 404 Not Found\r\nContent-Length: 0\r\n\r\n");
            			netStream.Write(wr,0,wr.Length);
				netStream.Close();  client.Close();
				return;
			}

			netStream.Close();
			client.Close();

		}

		public  bool IgnoreCertificateValidation (System.Security.Cryptography.X509Certificates.X509Certificate certificate, int[] certificateErrors) {
			return true;
		}

		private class CapInfo {
			private string uri; 
			private IPEndPoint sim; 
			private string type;
			public CapInfo(string URI, IPEndPoint Sim, string CapType) {
				uri = URI; sim = Sim; type = CapType;
			}
			public string URI {
				get { return uri; }
			}
			public string CapType {
				get { return type; }
			}
			public IPEndPoint Sim {
				get { return sim; }
			}
		}

		private Dictionary<string,CapInfo> KnownCaps;

		private void ProxyCaps(NetworkStream netStream, string meth, string uri, Dictionary<string,string> headers, byte[] content,int reqNo) {
			string host; int port; string path; string proto;
			string line;
			Match match = new Regex(@"^(https?)://([^:/]+)(:\d+)?(/.*)$").Match(uri);
			if(!match.Success) {
				Console.WriteLine("["+reqNo+"] Malformed proxy URI: "+uri);
				byte[] wr = Encoding.ASCII.GetBytes("HTTP/1.0 404 Not Found\r\nContent-Length: 0\r\n\r\n");
            			netStream.Write(wr,0,wr.Length);
				return;
			}
			proto = match.Groups[1].Captures[0].ToString();
			host = match.Groups[2].Captures[0].ToString();
			path = match.Groups[4].Captures[0].ToString();
			if(match.Groups[3].Captures.Count > 0) {
				port = Convert.ToInt32( match.Groups[3].Captures[0].ToString().Substring(1));
			} else if(proto == "https") {
				port = 443;
			} else {
				port = 80;
			}

			WebRequest req = WebRequest.Create(uri);
			foreach(string header in headers.Keys) {
				if(header=="accept" || header=="connection" || 
				   header=="content-length" || header=="date" || header=="expect" ||
				   header=="host" || header=="if-modified-since" || header=="referer" || 
				   header=="transfer-encoding" || header=="user-agent" || 
				   header=="proxy-connection") {
					// can't touch these!
				} else if(header == "content-type") {
					req.ContentType = headers["content-type"];
				} else {
					req.Headers[header] = headers[header];
				}
			}
			req.Method = meth; req.ContentLength = content.Length;
			Stream reqStream = req.GetRequestStream();
			reqStream.Write(content,0,content.Length);
			reqStream.Close();
			HttpWebResponse resp;
			try {
				resp = (HttpWebResponse)req.GetResponse();
			} catch(WebException e) {
				if(e.Status == WebExceptionStatus.Timeout || e.Status == WebExceptionStatus.SendFailure) {
					Console.WriteLine("Request timeout");
					byte[] wr = Encoding.ASCII.GetBytes("HTTP/1.0 504 Proxy Request Timeout\r\nContent-Length: 0\r\n\r\n");
            				netStream.Write(wr,0,wr.Length);
					return;
				} else if(e.Status == WebExceptionStatus.ProtocolError && e.Response != null) {
					resp = (HttpWebResponse)e.Response;
				} else {
					Console.WriteLine("Request error: "+e.ToString());
					byte[] wr = Encoding.ASCII.GetBytes("HTTP/1.0 502 Gateway Error\r\nContent-Length: 0\r\n\r\n"); // FIXME
            				netStream.Write(wr,0,wr.Length);
					return;

				}
				
			}

			Stream respStream = resp.GetResponseStream();
			int read; int length = 0;
			byte[] respBuf = new byte[256];
			do {
				read = respStream.Read(respBuf,length,256);
				if(read > 0) {
					length += read;
					Array.Resize(ref respBuf,length+256);
				}
			} while(read > 0);
			Array.Resize(ref respBuf,length);

			string consoleMsg = "["+reqNo+"] Response from "+uri+"\nStatus: "+(int)resp.StatusCode+" "+resp.StatusDescription + "\n";

			{
				byte[] wr = Encoding.UTF8.GetBytes("HTTP/1.0 "+(int)resp.StatusCode+" "+resp.StatusDescription+"\r\n");
				netStream.Write(wr,0,wr.Length);
			}

			for(int i = 0; i < resp.Headers.Count; i++) {
				string key = resp.Headers.Keys[i];
				string val = resp.Headers[i];
				string lkey = key.ToLower();
				if(lkey != "content-length" && lkey != "transfer-encoding" && lkey != "connection") {
					consoleMsg += key+": "+val+"\n";
					byte[] wr = Encoding.UTF8.GetBytes(key+": "+val+"\r\n");
					netStream.Write(wr,0,wr.Length);
				}
			}

			consoleMsg += "\n"+Encoding.UTF8.GetString(respBuf)+"\n--------";
			Console.WriteLine(consoleMsg);

			try {
				if(resp.StatusCode == HttpStatusCode.OK) respBuf = CapsFixup(uri,respBuf);
			} catch(Exception e) {
				Console.WriteLine("["+reqNo+"] Couldn't fixup response: "+e.ToString());
			}

			Console.WriteLine("["+reqNo+"] Fixed-up response:\n"+Encoding.UTF8.GetString(respBuf)+"\n--------");

	
			{
				byte[] wr = Encoding.UTF8.GetBytes("Content-Length: "+respBuf.Length+"\r\n\r\n");
				netStream.Write(wr,0,wr.Length);
			}


			netStream.Write(respBuf,0,respBuf.Length);
			
			return;
		}

		private byte[] CapsFixup(string uri, byte[] data) {
			if(!KnownCaps.ContainsKey(uri)) {
				Console.WriteLine("Unknown caps URI: "+uri);
				return data;
			}
			CapInfo cap = KnownCaps[uri];
			object resp = LLSD.LLSDDeserialize(data);
			if(cap.CapType == "SeedCapability") {
				Hashtable m = (Hashtable)resp;
				Hashtable nm = new Hashtable();
				foreach(string key in m.Keys) {
					string val = (string)m[key];
					if(val != null && val != "") {
						KnownCaps[val] = new CapInfo(val, cap.Sim, key);
						nm[key] = "http://127.0.0.1:8080/"+val;
					} else {
						nm[key] = val;
					}
				}
				resp = nm;
			} else if(cap.CapType == "EventQueueGet") {
				Console.WriteLine(LLSD.LLSDDump(resp,0));
				foreach(Hashtable evt in (ArrayList)((Hashtable)resp)["events"]) {
					string message = (string)evt["message"];
					Hashtable body = (Hashtable)evt["body"];
					if(message == "TeleportFinish" || message == "CrossedRegion") {
						Hashtable info = null;
						if(message == "TeleportFinish")
							info = (Hashtable) body["Info"];
						else
							info = (Hashtable) body["RegionData"];
						byte[] bytes = (byte[]) info["SimIP"];
						uint simIP = (uint)(bytes[3] + (bytes[2] << 8) + (bytes[1] << 16) + (bytes[0] << 24));
						ushort simPort = (ushort)(long)info["SimPort"];
						string capsURL = (string)info["SeedCapability"];
						GenericCheck(ref simIP, ref simPort, ref capsURL, cap.Sim == activeCircuit);
						info["SeedCapability"] = capsURL;
						bytes[3] = (byte)(simIP % 256);
				                bytes[2] = (byte)((simIP >> 8) % 256);
				                bytes[1] = (byte)((simIP >> 16) % 256);
				                bytes[0] = (byte)((simIP >> 24) % 256);
						info["SimIP"] = bytes;
						info["SimPort"] = (long)simPort;
					}
				}
			}
			return LLSD.LLSDSerialize(resp);
		}

		private void ProxyLogin(NetworkStream netStream, byte[] content) { lock(this) {
			// convert the body into an XML-RPC request
			XmlRpcRequest request = (XmlRpcRequest)(new XmlRpcRequestDeserializer()).Deserialize(Encoding.UTF8.GetString(content));

			// call the loginRequestDelegate
			if (loginRequestDelegate != null)
				try {
					loginRequestDelegate(request);
				} catch (Exception e) {
					Log("exception in login request deligate: " + e.Message, true);
					Log(e.StackTrace, true);
				}

			// add our userAgent and author to the request
			Hashtable requestParams = new Hashtable();
			if (proxyConfig.userAgent != null)
				requestParams["user-agent"] = proxyConfig.userAgent;
			if (proxyConfig.author != null)
				requestParams["author"] = proxyConfig.author;
			request.Params.Add(requestParams);

			// forward the XML-RPC request to the server
			XmlRpcResponse response = (XmlRpcResponse)request.Send(proxyConfig.remoteLoginUri.ToString(),60000); //added 60 second timeout -- Andrew
			Hashtable responseData = (Hashtable)response.Value;

			// proxy any simulator address given in the XML-RPC response
			if (responseData.Contains("sim_ip") && responseData.Contains("sim_port")) {
				IPEndPoint realSim = new IPEndPoint(IPAddress.Parse((string)responseData["sim_ip"]), Convert.ToUInt16(responseData["sim_port"]));
				IPEndPoint fakeSim = ProxySim(realSim);
				responseData["sim_ip"] = fakeSim.Address.ToString();
				responseData["sim_port"] = fakeSim.Port;
				activeCircuit = realSim;
			}

			// start a new proxy session
			Reset();

			if(responseData.Contains("seed_capability")) {
				KnownCaps[(string)responseData["seed_capability"]] = new CapInfo((string)responseData["seed_capability"],activeCircuit,"SeedCapability");
				responseData["seed_capability"] = "http://127.0.0.1:8080/"+responseData["seed_capability"];
			}

			// call the loginResponseDelegate
			if (loginResponseDelegate != null) {
				try {
					loginResponseDelegate(response);
				} catch (Exception e) {
					Log("exception in login response delegate: " + e.Message, true);
					Log(e.StackTrace, true);
				}
			}

			// forward the XML-RPC response to the client
			StreamWriter writer = new StreamWriter(netStream);
		        writer.WriteLine("HTTP/1.0 200 OK");
            		writer.WriteLine("Content-type: text/xml");
            		writer.WriteLine();

			XmlTextWriter responseWriter = new XmlTextWriter(writer);
			XmlRpcResponseSerializer.Singleton.Serialize(responseWriter, response);
			responseWriter.Close(); writer.Close();
		}}

		/*
		 * Sim Proxy
		 */

		private Socket simFacingSocket;
		private IPEndPoint activeCircuit = null;
		private Hashtable proxyEndPoints = new Hashtable();
		private Hashtable simProxies = new Hashtable();
		private Hashtable proxyHandlers = new Hashtable();
		private XmlRpcRequestDelegate loginRequestDelegate = null;
		private XmlRpcResponseDelegate loginResponseDelegate = null;
		private Dictionary<PacketType, List<PacketDelegate>> incomingDelegates = new Dictionary<PacketType, List<PacketDelegate>>();
		private Dictionary<PacketType, List<PacketDelegate>> outgoingDelegates = new Dictionary<PacketType, List<PacketDelegate>>();
		private ArrayList queuedIncomingInjections = new ArrayList();
		private ArrayList queuedOutgoingInjections = new ArrayList();

		// InitializeSimProxy: initialize the sim proxy
		private void InitializeSimProxy() {
			InitializeAddressCheckers();

			simFacingSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			simFacingSocket.Bind(new IPEndPoint(proxyConfig.remoteFacingAddress, 0));
			Reset();
		}

		// Reset: start a new session
		private void Reset() {
			foreach (SimProxy simProxy in simProxies.Values)
				simProxy.Reset();
			KnownCaps = new Dictionary<string,CapInfo>();
		}

		private byte[] receiveBuffer = new byte[8192];
		private byte[] zeroBuffer = new byte[8192];
		private EndPoint remoteEndPoint = (EndPoint)new IPEndPoint(IPAddress.Any, 0);

		// RunSimProxy: start listening for packets from remote sims
		private void RunSimProxy() {
			simFacingSocket.BeginReceiveFrom(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, ref remoteEndPoint, new AsyncCallback(ReceiveFromSim), null);
		}

		// ReceiveFromSim: packet received from a remote sim
		private void ReceiveFromSim(IAsyncResult ar) { lock(this) try {
			// pause listening and fetch the packet
			bool needsZero = false;
			bool needsCopy = true;
			int length;
			length = simFacingSocket.EndReceiveFrom(ar, ref remoteEndPoint);

			if (proxyHandlers.Contains(remoteEndPoint)) {
				// find the proxy responsible for forwarding this packet
				SimProxy simProxy = (SimProxy)proxyHandlers[remoteEndPoint];

				// interpret the packet according to the SL protocol
				Packet packet;
                int end = length - 1;

                packet = Packet.BuildPacket(receiveBuffer, ref end, zeroBuffer);
#if DEBUG_SEQUENCE
				Console.WriteLine("<- " + packet.Type + " #" + packet.Header.Sequence);
#endif

				// check for ACKs we're waiting for
				packet = simProxy.CheckAcks(packet, Direction.Incoming, ref length, ref needsCopy);

				// modify sequence numbers to account for injections
				uint oldSequence = packet.Header.Sequence;
				packet = simProxy.ModifySequence(packet, Direction.Incoming, ref length, ref needsCopy);

				// keep track of sequence numbers
				if (packet.Header.Sequence > simProxy.incomingSequence)
					simProxy.incomingSequence = packet.Header.Sequence;

				// check the packet for addresses that need proxying
				if (incomingCheckers.Contains(packet.Type)) {
					/* if (needsZero) {
						length = Helpers.ZeroDecode(packet.Header.Data, length, zeroBuffer);
						packet.Header.Data = zeroBuffer;
						needsZero = false;
					} */

					Packet newPacket = ((AddressChecker)incomingCheckers[packet.Type])(packet);
					SwapPacket(packet, newPacket);
					packet = newPacket;
					needsCopy = false;
				}

				// pass the packet to any callback delegates
				if (incomingDelegates.ContainsKey(packet.Type)) {
					/* if (needsZero) {
						length = Helpers.ZeroDecode(packet.Header.Data, length, zeroBuffer);
						packet.Header.Data = zeroBuffer;
						needsCopy = true;
					} */

					if (needsCopy) {
						byte[] newData = new byte[packet.Header.Data.Length];
						Array.Copy(packet.Header.Data, 0, newData, 0, packet.Header.Data.Length);
						packet.Header.Data = newData; // FIXME
					}

					try {
						Packet newPacket = callDelegates(incomingDelegates, packet, (IPEndPoint)remoteEndPoint);
						if (newPacket == null) {
							if ((packet.Header.Flags & Helpers.MSG_RELIABLE) != 0)
								simProxy.Inject(SpoofAck(oldSequence), Direction.Outgoing);

							if ((packet.Header.Flags & Helpers.MSG_APPENDED_ACKS) != 0)
								packet = SeparateAck(packet);
							else
								packet = null;
						} else {
							bool oldReliable = (packet.Header.Flags & Helpers.MSG_RELIABLE) != 0;
							bool newReliable = (newPacket.Header.Flags & Helpers.MSG_RELIABLE) != 0;
							if (oldReliable && !newReliable)
								simProxy.Inject(SpoofAck(oldSequence), Direction.Outgoing);
							else if (!oldReliable && newReliable)
								simProxy.WaitForAck(packet, Direction.Incoming);

							SwapPacket(packet, newPacket);
							packet = newPacket;
						}
					} catch (Exception e) {
						Log("exception in incoming delegate: " + e.Message, true);
						Log(e.StackTrace, true);
					}

					if (packet != null)
						simProxy.SendPacket(packet, false);
				} else
					simProxy.SendPacket(packet, needsZero);
			} else
				// ignore packets from unknown peers
				Log("dropping packet from " + remoteEndPoint, false);
		} catch (Exception e) {
			Console.WriteLine(e.Message);
			Console.WriteLine(e.StackTrace);
		} finally {
			// resume listening
			simFacingSocket.BeginReceiveFrom(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, ref remoteEndPoint, new AsyncCallback(ReceiveFromSim), null);
		}}

		// SendPacket: send a packet to a sim from our fake client endpoint
		public void SendPacket(Packet packet, IPEndPoint endPoint, bool skipZero) {
			byte[] buffer = packet.ToBytes();
			if (skipZero || (packet.Header.Data[0] & Helpers.MSG_ZEROCODED) == 0)
				simFacingSocket.SendTo(buffer, buffer.Length, SocketFlags.None, endPoint);
			else {
				int zeroLength = Helpers.ZeroEncode(buffer, buffer.Length, zeroBuffer);
				simFacingSocket.SendTo(zeroBuffer, zeroLength, SocketFlags.None, endPoint);
			}
			
		}

		// SpoofAck: create an ACK for the given packet
		public Packet SpoofAck(uint sequence) {
            PacketAckPacket spoof = new PacketAckPacket();
            spoof.Packets = new PacketAckPacket.PacketsBlock[1];
	    spoof.Packets[0] = new PacketAckPacket.PacketsBlock();
            spoof.Packets[0].ID = sequence;
            return (Packet)spoof;
            //Legacy:
            ////Hashtable blocks = new Hashtable();
            ////Hashtable fields = new Hashtable();
            ////fields["ID"] = (uint)sequence;
            ////blocks[fields] = "Packets";
            ////return .BuildPacket("PacketAck", proxyConfig.protocol, blocks, Helpers.MSG_ZEROCODED);
		}

		// SeparateAck: create a standalone PacketAck for packet's appended ACKs
		public Packet SeparateAck(Packet packet) {
            PacketAckPacket seperate = new PacketAckPacket();
            int ackCount = ((packet.Header.Data[0] & Helpers.MSG_APPENDED_ACKS) == 0 ? 0 : (int)packet.Header.Data[packet.Header.Data.Length - 1]);
            seperate.Packets = new PacketAckPacket.PacketsBlock[ackCount];	
		
            for (int i = 0; i < ackCount; ++i)
            {
            	int offset = packet.Header.Data.Length - (ackCount - i) * 4 - 1;
                seperate.Packets[i].ID = (uint) ((packet.Header.Data[offset++] <<  0)
				                                + (packet.Header.Data[offset++] <<  8)
				                                + (packet.Header.Data[offset++] << 16)
				                                + (packet.Header.Data[offset++] << 24))
				                                ;
            }

            Packet ack = (Packet)seperate;
            ack.Header.Sequence = packet.Header.Sequence;
            return ack;
		}

		// SwapPacket: copy the sequence number and appended ACKs from one packet to another
		public static void SwapPacket(Packet oldPacket, Packet newPacket) {
			newPacket.Header.Sequence = oldPacket.Header.Sequence;

			int oldAcks = (oldPacket.Header.Data[0] & Helpers.MSG_APPENDED_ACKS) == 0 ? 0 : oldPacket.Header.AckList.Length;
			int newAcks = (newPacket.Header.Data[0] & Helpers.MSG_APPENDED_ACKS) == 0 ? 0 : newPacket.Header.AckList.Length;

			if (oldAcks != 0 || newAcks != 0) {
				
               			uint[] newAckList = new uint[oldAcks];
		                Array.Copy(oldPacket.Header.AckList, 0, newAckList, 0, oldAcks);

				newPacket.Header.AckList = newAckList;
				newPacket.Header.AppendedAcks = oldPacket.Header.AppendedAcks;
                
			}
		}

		// ProxySim: return the proxy for the specified sim, creating it if it doesn't exist
		private IPEndPoint ProxySim(IPEndPoint simEndPoint) {
			if (proxyEndPoints.Contains(simEndPoint))
				// return the existing proxy
				return (IPEndPoint)proxyEndPoints[simEndPoint];
			else {
				// return a new proxy
				SimProxy simProxy = new SimProxy(proxyConfig, simEndPoint, this);
				IPEndPoint fakeSim = simProxy.LocalEndPoint();
				Log("creating proxy for " + simEndPoint + " at " + fakeSim, false);
				simProxy.Run();
				proxyEndPoints.Add(simEndPoint, fakeSim);
				simProxies.Add(simEndPoint, simProxy);
				return fakeSim;
			}
		}

		// AddHandler: remember which sim proxy corresponds to a given sim
		private void AddHandler(EndPoint endPoint, SimProxy proxy) {
			proxyHandlers.Add(endPoint, proxy);
		}

		// SimProxy: proxy for a single simulator
		private class SimProxy {
			private ProxyConfig proxyConfig;
			private IPEndPoint remoteEndPoint;
			private Proxy proxy;
			private Socket socket;
			public uint incomingSequence;
			public uint outgoingSequence;
			private ArrayList incomingInjections;
			private ArrayList outgoingInjections;
			private uint incomingOffset = 0;
			private uint outgoingOffset = 0;
			private Hashtable incomingAcks;
			private Hashtable outgoingAcks;
			private ArrayList incomingSeenAcks;
			private ArrayList outgoingSeenAcks;

			// SimProxy: construct a proxy for a single simulator
			public SimProxy(ProxyConfig proxyConfig, IPEndPoint simEndPoint, Proxy proxy) {
				this.proxyConfig = proxyConfig;
				remoteEndPoint = new IPEndPoint(simEndPoint.Address, simEndPoint.Port);
				this.proxy = proxy;
				socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
				socket.Bind(new IPEndPoint(proxyConfig.clientFacingAddress, 0));
				proxy.AddHandler(remoteEndPoint, this);
				Reset();
			}

			// Reset: start a new session
			public void Reset() {
				incomingSequence = 0;
				outgoingSequence = 0;
				incomingInjections = new ArrayList();
				outgoingInjections = new ArrayList();
				incomingAcks = new Hashtable();
				outgoingAcks = new Hashtable();
				incomingSeenAcks = new ArrayList();
				outgoingSeenAcks = new ArrayList();
			}

			// BackgroundTasks: resend unacknowledged packets and keep data structures clean
			private void BackgroundTasks() { try {
				int tick = 1;
				int incomingInjectionsPoint = 0;
				int outgoingInjectionsPoint = 0;
				int incomingSeenAcksPoint = 0;
				int outgoingSeenAcksPoint = 0;

				for (;; Thread.Sleep(1000)) lock(proxy) {
					if ((tick = (tick + 1) % 60) == 0) {
						for (int i = 0; i < incomingInjectionsPoint; ++i) {
							incomingInjections.RemoveAt(0);
							++incomingOffset;
#if DEBUG_SEQUENCE
							Console.WriteLine("incomingOffset = " + incomingOffset);
#endif
						}
						incomingInjectionsPoint = incomingInjections.Count;

						for (int i = 0; i < outgoingInjectionsPoint; ++i) {
							outgoingInjections.RemoveAt(0);
							++outgoingOffset;
#if DEBUG_SEQUENCE
							Console.WriteLine("outgoingOffset = " + outgoingOffset);
#endif
						}
						outgoingInjectionsPoint = outgoingInjections.Count;

						for (int i = 0; i < incomingSeenAcksPoint; ++i) {
#if DEBUG_SEQUENCE
							Console.WriteLine("incomingAcks.Remove(" + incomingSeenAcks[0] + ")");
#endif
							incomingAcks.Remove(incomingSeenAcks[0]);
							incomingSeenAcks.RemoveAt(0);
						}
						incomingSeenAcksPoint = incomingSeenAcks.Count;

						for (int i = 0; i < outgoingSeenAcksPoint; ++i) {
#if DEBUG_SEQUENCE
							Console.WriteLine("outgoingAcks.Remove(" + outgoingSeenAcks[0] + ")");
#endif
							outgoingAcks.Remove(outgoingSeenAcks[0]);
							outgoingSeenAcks.RemoveAt(0);
						}
						outgoingSeenAcksPoint = outgoingSeenAcks.Count;
					}

					foreach (uint id in incomingAcks.Keys)
						if (!incomingSeenAcks.Contains(id)) {
							Packet packet = (Packet)incomingAcks[id];
							packet.Header.Data[0] |= Helpers.MSG_RESENT;
#if DEBUG_SEQUENCE
							Console.WriteLine("RESEND <- " + packet.Type + " #" + packet.Header.Sequence);
#endif
							SendPacket(packet, false);
						}

					foreach (uint id in outgoingAcks.Keys)
						if (!outgoingSeenAcks.Contains(id)) {
							Packet packet = (Packet)outgoingAcks[id];
							packet.Header.Data[0] |= Helpers.MSG_RESENT;
#if DEBUG_SEQUENCE
							Console.WriteLine("RESEND -> " + packet.Type + " #" + packet.Header.Sequence);
#endif
							proxy.SendPacket(packet, remoteEndPoint, false);
						}
				}
			} catch (Exception e) {
				Console.WriteLine(e.Message);
				Console.WriteLine(e.StackTrace);
			}}

			// LocalEndPoint: return the endpoint that the client should communicate with
			public IPEndPoint LocalEndPoint() {
				return (IPEndPoint)socket.LocalEndPoint;
			}

			private byte[] receiveBuffer = new byte[8192];
			private byte[] zeroBuffer = new byte[8192];
			private EndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
			bool firstReceive = true;

			// Run: forward packets from the client to the sim
			public void Run() {
				Thread backgroundTasks = new Thread(new ThreadStart(BackgroundTasks));
				backgroundTasks.IsBackground = true;
				backgroundTasks.Start();
				socket.BeginReceiveFrom(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, ref clientEndPoint, new AsyncCallback(ReceiveFromClient), null);
			}

			// ReceiveFromClient: packet received from the client
			private void ReceiveFromClient(IAsyncResult ar) { lock(proxy) try {
				// pause listening and fetch the packet
				bool needsZero = false;
				bool needsCopy = true;
				int length;
				length = socket.EndReceiveFrom(ar, ref clientEndPoint);

				// interpret the packet according to the SL protocol
		                int end = length - 1;
				Packet packet = libsecondlife.Packets.Packet.BuildPacket(receiveBuffer,ref end, zeroBuffer);
				
#if DEBUG_SEQUENCE
				Console.WriteLine("-> " + packet.Type + " #" + packet.Header.Sequence);
#endif
                // check for ACKs we're waiting for
				packet = CheckAcks(packet, Direction.Outgoing, ref length, ref needsCopy);

				// modify sequence numbers to account for injections
				uint oldSequence = packet.Header.Sequence;
				packet = ModifySequence(packet, Direction.Outgoing, ref length, ref needsCopy);

				// keep track of sequence numbers
				if (packet.Header.Sequence > outgoingSequence)
                    outgoingSequence = packet.Header.Sequence ;

				// check the packet for addresses that need proxying
				if (proxy.outgoingCheckers.Contains(packet.Type)) {
					/* if (packet.Header.Zerocoded) {
						length = Helpers.ZeroDecode(packet.Header.Data, length, zeroBuffer);
						packet.Header.Data = zeroBuffer;
						needsZero = false;
					} */

					Packet newPacket = ((AddressChecker)proxy.outgoingCheckers[packet.Type])(packet);
					SwapPacket(packet, newPacket);
					packet = newPacket;
					length = packet.Header.Data.Length;
					needsCopy = false;
				}

				// pass the packet to any callback delegates
				if (proxy.outgoingDelegates.ContainsKey(packet.Type)) {
					/* if (packet.Header.Zerocoded) {
						length = Helpers.ZeroDecode(packet.Header.Data, length, zeroBuffer);
						packet.Header.Data = zeroBuffer;
						needsCopy = true;
					} */

					if (needsCopy) {
						byte[] newData = new byte[packet.Header.Data.Length];
						Array.Copy(packet.Header.Data, 0, newData, 0, packet.Header.Data.Length);
						packet.Header.Data = newData; // FIXME!!!
					}

					try {
						Packet newPacket = proxy.callDelegates(proxy.outgoingDelegates, packet, remoteEndPoint);
						if (newPacket == null) {
							if ((packet.Header.Flags & Helpers.MSG_RELIABLE) != 0)
								Inject(proxy.SpoofAck(oldSequence), Direction.Incoming);

							if ((packet.Header.Flags & Helpers.MSG_APPENDED_ACKS) != 0)
								packet = proxy.SeparateAck(packet);
							else
								packet = null;
						} else {
							bool oldReliable = (packet.Header.Flags & Helpers.MSG_RELIABLE) != 0;
							bool newReliable = (newPacket.Header.Flags & Helpers.MSG_RELIABLE) != 0;
							if (oldReliable && !newReliable)
								Inject(proxy.SpoofAck(oldSequence), Direction.Incoming);
							else if (!oldReliable && newReliable)	
								WaitForAck(packet, Direction.Outgoing);

							SwapPacket(packet, newPacket);
							packet = newPacket;
						}
					} catch (Exception e) {
						proxy.Log("exception in outgoing delegate: " + e.Message, true);
						proxy.Log(e.StackTrace, true);
					}

					if (packet != null)
						proxy.SendPacket(packet, remoteEndPoint, false);
				} else
					proxy.SendPacket(packet, remoteEndPoint, needsZero);

				// send any packets queued for injection
				if (firstReceive) {
					firstReceive = false;
					foreach (Packet queuedPacket in proxy.queuedIncomingInjections)
						Inject(queuedPacket, Direction.Incoming);
					proxy.queuedIncomingInjections = new ArrayList();
				}
			} catch (Exception e) {
				Console.WriteLine(e.Message);
				Console.WriteLine(e.StackTrace);
			} finally {
				// resume listening
				socket.BeginReceiveFrom(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, ref clientEndPoint, new AsyncCallback(ReceiveFromClient), null);
			}}

			// SendPacket: send a packet from the sim to the client via our fake sim endpoint
			public void SendPacket(Packet packet, bool skipZero) {
				byte[] buffer = packet.ToBytes();
				if (skipZero || (packet.Header.Data[0] & Helpers.MSG_ZEROCODED) == 0)
					socket.SendTo(buffer, buffer.Length, SocketFlags.None, clientEndPoint);
				else {
					int zeroLength = Helpers.ZeroEncode(buffer, buffer.Length, zeroBuffer);
					socket.SendTo(zeroBuffer, zeroLength, SocketFlags.None, clientEndPoint);
				}
			}

			// Inject: inject a packet
			public void Inject(Packet packet, Direction direction) {
				if (direction == Direction.Incoming) {
					if (firstReceive) {
						proxy.queuedIncomingInjections.Add(packet);
						return;
					}

					incomingInjections.Add(++incomingSequence);
					packet.Header.Sequence = incomingSequence;
				} else {
					outgoingInjections.Add(++outgoingSequence);
					packet.Header.Sequence = outgoingSequence;
				}

#if DEBUG_SEQUENCE
				Console.WriteLine("INJECT " + (direction == Direction.Incoming ? "<-" : "->") + " " + packet.Type + " #" + packet.Header.Sequence);

#endif
				if ((packet.Header.Data[0] & Helpers.MSG_RELIABLE) != 0)
					WaitForAck(packet, direction);

				if (direction == Direction.Incoming) {
					byte[] buffer = packet.ToBytes();
					if ((packet.Header.Data[0] & Helpers.MSG_ZEROCODED) == 0)
						socket.SendTo(buffer, buffer.Length, SocketFlags.None, clientEndPoint);
					else {
						int zeroLength = Helpers.ZeroEncode(buffer, buffer.Length, zeroBuffer);
						socket.SendTo(zeroBuffer, zeroLength, SocketFlags.None, clientEndPoint);
					}
				}
				else
					proxy.SendPacket(packet, remoteEndPoint, false);
			}

			// WaitForAck: take care of resending a packet until it's ACKed
			public void WaitForAck(Packet packet, Direction direction) {
				Hashtable table = direction == Direction.Incoming ? incomingAcks : outgoingAcks;
				table.Add(packet.Header.Sequence, packet);
			}

			// CheckAcks: check for and remove ACKs of packets we've injected
			public Packet CheckAcks(Packet packet, Direction direction, ref int length, ref bool needsCopy) {
				Hashtable acks = direction == Direction.Incoming ? outgoingAcks : incomingAcks;
				ArrayList seenAcks = direction == Direction.Incoming ? outgoingSeenAcks : incomingSeenAcks;

				if (acks.Count == 0)
					return packet;

				// check for embedded ACKs
				if (packet.Type == PacketType.PacketAck) {
					bool changed = false;
                    List<PacketAckPacket.PacketsBlock> newPacketBlocks = new List<PacketAckPacket.PacketsBlock>();
					foreach (PacketAckPacket.PacketsBlock pb in ((PacketAckPacket)packet).Packets) {
						uint id = pb.ID;
#if DEBUG_SEQUENCE
						string hrup = "Check !" + id;
#endif
                        if (acks.Contains(id))
                        {
#if DEBUG_SEQUENCE
							hrup += " get's";
#endif
                            seenAcks.Add(id);
                            changed = true;
                        }
                        else
                            newPacketBlocks.Add(pb);
#if DEBUG_SEQUENCE
						Console.WriteLine(hrup);
#endif
					}
					if (changed)
                    {
                        PacketAckPacket newPacket = new PacketAckPacket();
                        newPacket.Packets = new PacketAckPacket.PacketsBlock[newPacketBlocks.Count];
                        
                        int a = 0;
                        foreach (PacketAckPacket.PacketsBlock pb in newPacketBlocks)
                        {
                            newPacket.Packets[a++] = pb;
                        } 

                        SwapPacket(packet, (Packet)newPacket);
						packet = newPacket;
						length = packet.Header.Data.Length;
						needsCopy = false;
					}
				}

				// check for appended ACKs
				if ((packet.Header.Data[0] & Helpers.MSG_APPENDED_ACKS) != 0) {
					int ackCount = packet.Header.AckList.Length;
					for (int i = 0; i < ackCount;) {
						uint ackID = packet.Header.AckList[i]; // FIXME FIXME FIXME
#if DEBUG_SEQUENCE
						string hrup = "Check @" + ackID;
#endif
						if (acks.Contains(ackID)) {
#if DEBUG_SEQUENCE
							hrup += " get's";
#endif
							uint[] newAcks = new uint[ackCount-1];
							Array.Copy(packet.Header.AckList, 0, newAcks, 0, i);
							Array.Copy(packet.Header.AckList, i+1, newAcks, i, ackCount - i - 1);
							packet.Header.AckList = newAcks;
							--ackCount;
							seenAcks.Add(ackID);
							needsCopy = false;
						} else
							++i;
#if DEBUG_SEQUENCE
						Console.WriteLine(hrup);
#endif
					}
					if (ackCount == 0) {
						packet.Header.Flags ^= Helpers.MSG_APPENDED_ACKS;
						packet.Header.AckList = new uint[0];
					}
				}

				return packet;
			}

			// ModifySequence: modify a packet's sequence number and ACK IDs to account for injections
			public Packet ModifySequence(Packet packet, Direction direction, ref int length, ref bool needsCopy) {
				ArrayList ourInjections = direction == Direction.Outgoing ? outgoingInjections : incomingInjections;
				ArrayList theirInjections = direction == Direction.Incoming ? outgoingInjections : incomingInjections;
				uint ourOffset = direction == Direction.Outgoing ? outgoingOffset : incomingOffset;
				uint theirOffset = direction == Direction.Incoming ? outgoingOffset : incomingOffset;

				uint newSequence = (uint)(packet.Header.Sequence + ourOffset);
				foreach (uint injection in ourInjections)
					if (newSequence >= injection)
						++newSequence;
#if DEBUG_SEQUENCE
				Console.WriteLine("Mod #" + packet.Header.Sequence + " = " + newSequence);
#endif
				packet.Header.Sequence = newSequence;

				if ((packet.Header.Flags & Helpers.MSG_APPENDED_ACKS) != 0) {
					int ackCount = packet.Header.AckList.Length;
					for (int i = 0; i < ackCount; ++i) {
						int offset = length - (ackCount - i) * 4 - 1;
						uint ackID = packet.Header.AckList[i] - theirOffset;
#if DEBUG_SEQUENCE
						uint hrup = packet.Header.AckList[i];
#endif
						for (int j = theirInjections.Count - 1; j >= 0; --j)
							if (ackID >= (uint)theirInjections[j])
								--ackID;
#if DEBUG_SEQUENCE
						Console.WriteLine("Mod @" + hrup + " = " + ackID);
#endif
						packet.Header.AckList[i] = ackID;
					}
				}

				if (packet.Type == PacketType.PacketAck) {
                    PacketAckPacket pap = (PacketAckPacket)packet;
                    foreach(PacketAckPacket.PacketsBlock pb in pap.Packets) {
                    	uint ackID = (uint)pb.ID - theirOffset;
#if DEBUG_SEQUENCE
						uint hrup = (uint)pb.ID;
#endif
						for (int i = theirInjections.Count - 1; i >= 0; --i)
							if (ackID >= (uint)theirInjections[i])
								--ackID;
#if DEBUG_SEQUENCE
						Console.WriteLine("Mod !" + hrup + " = " + ackID);
#endif
						pb.ID = ackID;
					
					}
                    //SwapPacket(packet, (Packet)pap);
					// packet = (Packet)pap;
					length = packet.Header.Data.Length;
					needsCopy = false;
				}

				return packet;
			}
		}

		// Checkers swap proxy addresses in for real addresses.  A few constraints:
		//   - Checkers must not alter the incoming packet.
		//   - Checkers must return a freshly built packet, even if nothing's changed.
		//   - The incoming packet's buffer may be longer than the length of the data it contains.
		//   - The incoming packet's buffer must not be used after the checker returns.
		// This is all because checkers may be operating on data that's still in a scratch buffer.
		delegate Packet AddressChecker(Packet packet);

		Hashtable incomingCheckers = new Hashtable();
		Hashtable outgoingCheckers = new Hashtable();

		// InitializeAddressCheckers: initialize delegates that check packets for addresses that need proxying
		private void InitializeAddressCheckers() {
			// TODO: what do we do with mysteries and empty IPs?
			AddMystery(PacketType.OpenCircuit);
			//AddMystery(PacketType.AgentPresenceResponse);

			incomingCheckers.Add(PacketType.TeleportFinish, new AddressChecker(CheckTeleportFinish));
			incomingCheckers.Add(PacketType.AgentToNewRegion, new AddressChecker(CheckAgentToNewRegion));
			incomingCheckers.Add(PacketType.CrossedRegion, new AddressChecker(CheckCrossedRegion));
			incomingCheckers.Add(PacketType.EnableSimulator, new AddressChecker(CheckEnableSimulator));
			//incomingCheckers.Add("UserLoginLocationReply", new AddressChecker(CheckUserLoginLocationReply));
		}

		// AddMystery: add a checker delegate that logs packets we're watching for development purposes
		private void AddMystery(PacketType type) {
			incomingCheckers.Add(type, new AddressChecker(LogIncomingMysteryPacket));
			outgoingCheckers.Add(type, new AddressChecker(LogOutgoingMysteryPacket));
		}

		// GenericCheck: replace the sim address in a packet with our proxy address
		private void GenericCheck(ref uint simIP, ref ushort simPort, ref string simCaps, bool active) {
            	    IPAddress sim_ip = new IPAddress((long)simIP);

		    IPEndPoint realSim = new IPEndPoint(sim_ip, Convert.ToInt32(simPort));
	 	    IPEndPoint fakeSim = ProxySim(realSim);

		    if(simCaps != null && simCaps.Length > 0) {
			    KnownCaps[simCaps] = new CapInfo(simCaps,realSim,"SeedCapability");
			    simCaps = "http://127.0.0.1:8080/"+simCaps;
		    }

		    Console.WriteLine("FIXUP: "+realSim.ToString()+" -> "+fakeSim.ToString());

	            simPort = (ushort)fakeSim.Port;
        	    int i = 0;
	            byte[] bytes = fakeSim.Address.GetAddressBytes();
        	    simIP = (uint)(bytes[i++] + (bytes[i++] << 8) + (bytes[i++] << 16) + (bytes[i++] << 24));
            
	            if (active)
        	        activeCircuit = realSim;
		}

		// CheckTeleportFinish: check TeleportFinish packets
		private Packet CheckTeleportFinish(Packet packet) {
            		TeleportFinishPacket tfp = (TeleportFinishPacket)packet;
			string simCaps = Encoding.UTF8.GetString(tfp.Info.SeedCapability).Replace("\0","");
		        GenericCheck(ref tfp.Info.SimIP, ref tfp.Info.SimPort, ref simCaps, true);
			tfp.Info.SeedCapability = Encoding.UTF8.GetBytes(simCaps);
            		return (Packet)tfp;
		}

		// CheckAgentToNewRegion: check AgentToNewRegion packets
		private Packet CheckAgentToNewRegion(Packet packet) {
            		AgentToNewRegionPacket atnwp = (AgentToNewRegionPacket)packet;
			string simCaps = null; // FIXME: why doesn't this have a SeedCapability field?
            		GenericCheck(ref atnwp.RegionData.IP, ref atnwp.RegionData.Port, ref simCaps, true);
            		return (Packet)atnwp;
		}

		// CheckEnableSimulator: check EnableSimulator packets
		private Packet CheckEnableSimulator(Packet packet) {
            	EnableSimulatorPacket esp = (EnableSimulatorPacket)packet;
		string simCaps = null;
            	GenericCheck(ref esp.SimulatorInfo.IP, ref esp.SimulatorInfo.Port, ref simCaps, false);
            	return (Packet)esp;
		}

		// CheckCrossedRegion: check CrossedRegion packets
		private Packet CheckCrossedRegion(Packet packet) {
            	CrossedRegionPacket crp = (CrossedRegionPacket)packet;
		string simCaps = Encoding.UTF8.GetString(crp.RegionData.SeedCapability).Replace("\0","");
            	GenericCheck(ref crp.RegionData.SimIP, ref crp.RegionData.SimPort, ref simCaps, true);
		crp.RegionData.SeedCapability = Encoding.UTF8.GetBytes(simCaps);
            	return (Packet)crp;
		}

        // LogPacket: log a packet dump
		private Packet LogPacket(Packet packet, string type) {
			Log(type + " packet:", true);
			Log(packet, true);
            return packet;
		}

		// LogIncomingMysteryPacket: log an incoming packet we're watching for development purposes
		private Packet LogIncomingMysteryPacket(Packet packet) {
			return LogPacket(packet, "incoming mystery");
		}

		// LogOutgoingMysteryPacket: log an outgoing packet we're watching for development purposes
		private Packet LogOutgoingMysteryPacket(Packet packet) {
			return LogPacket(packet, "outgoing mystery");
		}
	}

	// XmlRpcRequestDelegate: specifies a delegate to be called for XML-RPC requests
	public delegate void XmlRpcRequestDelegate(XmlRpcRequest request);

	// XmlRpcResponseDelegate: specifies a delegate to be called for XML-RPC responses
	public delegate void XmlRpcResponseDelegate(XmlRpcResponse response);

	// PacketDelegate: specifies a delegate to be called when a packet passes through the proxy
	public delegate Packet PacketDelegate(Packet packet, IPEndPoint endPoint);

	// Direction: specifies whether a packet is going to the client (Incoming) or to a sim (Outgoing)
	public enum Direction {
		Incoming,
		Outgoing
	}
}
