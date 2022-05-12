using System;
using System.IO;
using System.Xml;
using System.Threading;

namespace Lidgren.Network
{
	/// <summary>
	/// Status of the UPnP capabilities
	/// </summary>
	public enum UPnPStatus
	{
		/// <summary>
		/// Still discovering UPnP capabilities
		/// </summary>
		Discovering,

		/// <summary>
		/// UPnP is not available
		/// </summary>
		NotAvailable,

		/// <summary>
		/// UPnP is available and ready to use
		/// </summary>
		Available
	}

	/// <summary>
	/// UPnP support class
	/// </summary>
	public class NetUPnP
	{
		private const int c_discoveryTimeOutMillis = 1000;

		private string m_serviceUrl;
		private string m_serviceName = "";
		private NetPeer m_peer;
		private ManualResetEvent m_discoveryComplete = new ManualResetEvent(false);

		internal double m_discoveryResponseDeadline;

		private UPnPStatus m_status = UPnPStatus.NotAvailable;

		/// <summary>
		/// Status of the UPnP capabilities of this NetPeer
		/// </summary>
		public UPnPStatus Status { get { return m_status; } }

		/// <summary>
		/// NetUPnP constructor
		/// </summary>
		public NetUPnP(NetPeer peer)
		{
			//
		}

		internal void Discover(NetPeer peer)
		{
			//
		}

	    internal void CheckForDiscoveryTimeout()
	    {
	        m_status = UPnPStatus.NotAvailable;
	    }

		internal void ExtractServiceUrl(string resp)
		{
			//
		}

		private bool CheckAvailability()
		{
			switch (m_status)
			{
				case UPnPStatus.NotAvailable:
					return false;
				case UPnPStatus.Available:
					return true;
				case UPnPStatus.Discovering:
					if (m_discoveryComplete.WaitOne(c_discoveryTimeOutMillis))
						return true;
					if (NetTime.Now > m_discoveryResponseDeadline)
						m_status = UPnPStatus.NotAvailable;
					return false;
			}
			return false;
        }

        /// <summary>
        /// Add a forwarding rule to the router using UPnP
        /// </summary>
        /// <param name="externalPort">The external, WAN facing, port</param>
        /// <param name="description">A description for the port forwarding rule</param>
        /// <param name="internalPort">The port on the client machine to send traffic to</param>
        public bool ForwardPort(int externalPort, string description, int internalPort = 0)
        {
            return false;
        }
	}
}