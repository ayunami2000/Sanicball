using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

public class WebSocket
{
    private Uri mUrl;

    public WebSocket(Uri url)
    {
        mUrl = url;

        var protocol = mUrl.Scheme;
        if (!protocol.Equals("ws") && !protocol.Equals("wss"))
            throw new ArgumentException("Unsupported protocol: " + protocol);
    }

    public void SendString(string str)
    {
        Send(Encoding.UTF8.GetBytes(str));
    }

    public string RecvString()
    {
        var retval = Recv();
        if (retval == null)
            return null;
        return Encoding.UTF8.GetString(retval);
    }

#if UNITY_WEBGL && !UNITY_EDITOR
	[DllImport("__Internal")]
	private static extern int SocketCreate (string url);

	[DllImport("__Internal")]
	private static extern int SocketState (int socketInstance);

	[DllImport("__Internal")]
	private static extern void SocketSend (int socketInstance, byte[] ptr, int length);

	[DllImport("__Internal")]
	private static extern void SocketRecv (int socketInstance, byte[] ptr, int length);

	[DllImport("__Internal")]
	private static extern int SocketRecvLength (int socketInstance);

	[DllImport("__Internal")]
	private static extern void SocketClose (int socketInstance);

	[DllImport("__Internal")]
	private static extern int SocketError (int socketInstance, byte[] ptr, int length);

	int m_NativeRef = 0;

	public void Send(byte[] buffer)
	{
		SocketSend (m_NativeRef, buffer, buffer.Length);
	}

	public byte[] Recv()
	{
		int length = SocketRecvLength (m_NativeRef);
		if (length == 0)
			return null;
		byte[] buffer = new byte[length];
		SocketRecv (m_NativeRef, buffer, length);
		return buffer;
	}

	public IEnumerator Connect()
	{
		m_NativeRef = SocketCreate (mUrl.ToString());

        int timer = 0;
		while (SocketState(m_NativeRef) == 0 && timer < 60*5)
        {
            timer++;
            yield return null;
        }
	}
 
	public void Close()
	{
		SocketClose(m_NativeRef);
	}

	public string error
	{
		get {
			const int bufsize = 1024;
			byte[] buffer = new byte[bufsize];
			int result = SocketError (m_NativeRef, buffer, bufsize);

			if (result == 0)
				return null;

			return Encoding.UTF8.GetString (buffer);				
		}
	}
#else
    public WebSocketSharp.WebSocket m_Socket;
    Queue<byte[]> m_Messages = new Queue<byte[]>();
    bool m_IsConnected = false;
    string m_Error = null;

    public IEnumerator Connect()
    {
        m_Socket = new WebSocketSharp.WebSocket(mUrl.ToString());
        m_Socket.OnMessage += M_Socket_OnMessage;
        m_Socket.OnOpen += M_Socket_OnOpen;
        m_Socket.OnError += M_Socket_OnError;
        m_Socket.Log.Level = WebSocketSharp.LogLevel.Debug;
        m_Socket.Log.Output = (sender, e) => Debug.Log(sender);
        m_Socket.ConnectAsync();
        int timer = 0;
        while (!m_IsConnected && timer < 60*5)
        {
            timer++;
            yield return null;
        }
        if (timer >= 60*5)
        {
             m_Error = "Timed out.";
        }
    }

    private void M_Socket_OnError(object sender, WebSocketSharp.ErrorEventArgs e)
    {
        Debug.Log("Websocket Errored");
        Debug.Log(e.Message);
        m_Error = e.Message;
    }

    private void M_Socket_OnOpen(object sender, EventArgs e)
    {
        Debug.Log("Websocket Open");
        m_IsConnected = true;
    }

    private void M_Socket_OnMessage(object sender, WebSocketSharp.MessageEventArgs e)
    {
        Debug.Log("Websocket Message");
        m_Messages.Enqueue(e.RawData);
    }

    public void Send(byte[] buffer)
    {
        try
        {
            m_Socket.Send(buffer);
        }
        catch (Exception ex)
        {
            m_Error = ex.Message;
        }
    }

    public byte[] Recv()
    {
        if (m_Messages.Count == 0)
            return null;
        return m_Messages.Dequeue();
    }

    public void Close()
    {
        m_Socket.Close();
    }

    public string error
    {
        get
        {
            return m_Error;
        }
    }
#endif 
}