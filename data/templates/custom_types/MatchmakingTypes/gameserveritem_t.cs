using System.Text;

namespace SwiftlyS2.Shared.SteamAPI;

/// <summary>
/// <para>Data describing a single server</para>
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 372, Pack = 4)]
public unsafe struct gameserveritem_t
{
	private static string ReadString( byte* buffer, int size )
	{
		var span = new ReadOnlySpan<byte>(buffer, size);
		var length = span.IndexOf((byte)0);
		return Encoding.UTF8.GetString(length < 0 ? span : span[..length]);
	}

	private static void WriteString( byte* buffer, int size, string value )
	{
		var span = new Span<byte>(buffer, size);
		span.Clear();
		var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
		bytes.AsSpan(0, Math.Min(bytes.Length, size - 1)).CopyTo(span);
	}

	public string GetGameDir()
	{
		fixed (byte* ptr = m_szGameDir)
			return ReadString(ptr, Constants.k_cbMaxGameServerGameDir);
	}

	public void SetGameDir( string dir )
	{
		fixed (byte* ptr = m_szGameDir)
			WriteString(ptr, Constants.k_cbMaxGameServerGameDir, dir);
	}

	public string GetMap()
	{
		fixed (byte* ptr = m_szMap)
			return ReadString(ptr, Constants.k_cbMaxGameServerMapName);
	}

	public void SetMap( string map )
	{
		fixed (byte* ptr = m_szMap)
			WriteString(ptr, Constants.k_cbMaxGameServerMapName, map);
	}

	public string GetGameDescription()
	{
		fixed (byte* ptr = m_szGameDescription)
			return ReadString(ptr, Constants.k_cbMaxGameServerGameDescription);
	}

	public void SetGameDescription( string desc )
	{
		fixed (byte* ptr = m_szGameDescription)
			WriteString(ptr, Constants.k_cbMaxGameServerGameDescription, desc);
	}

	public string GetServerName()
	{
		// Use the IP address as the name if nothing is set yet.
		if (m_szServerName[0] == 0)
			return m_NetAdr.GetConnectionAddressString();

		fixed (byte* ptr = m_szServerName)
			return ReadString(ptr, Constants.k_cbMaxGameServerName);
	}

	public void SetServerName( string name )
	{
		fixed (byte* ptr = m_szServerName)
			WriteString(ptr, Constants.k_cbMaxGameServerName, name);
	}

	public string GetGameTags()
	{
		fixed (byte* ptr = m_szGameTags)
			return ReadString(ptr, Constants.k_cbMaxGameServerTags);
	}

	public void SetGameTags( string tags )
	{
		fixed (byte* ptr = m_szGameTags)
			WriteString(ptr, Constants.k_cbMaxGameServerTags, tags);
	}

	/// <summary>
	/// <para>/&lt; IP/Query Port/Connection Port for this server</para>
	/// </summary>
	public servernetadr_t m_NetAdr;
	/// <summary>
	/// <para>/&lt; current ping time in milliseconds</para>
	/// </summary>
	public int m_nPing;
	[MarshalAs(UnmanagedType.I1)]
	/// <summary>
	/// <para>/&lt; server has responded successfully in the past</para>
	/// </summary>
	public bool m_bHadSuccessfulResponse;
	[MarshalAs(UnmanagedType.I1)]
	/// <summary>
	/// <para>/&lt; server is marked as not responding and should no longer be refreshed</para>
	/// </summary>
	public bool m_bDoNotRefresh;
	/// <summary>
	/// <para>/&lt; current game directory</para>
	/// </summary>
	private fixed byte m_szGameDir[Constants.k_cbMaxGameServerGameDir];
	/// <summary>
	/// <para>/&lt; current map</para>
	/// </summary>
	private fixed byte m_szMap[Constants.k_cbMaxGameServerMapName];
	/// <summary>
	/// <para>/&lt; game description</para>
	/// </summary>
	private fixed byte m_szGameDescription[Constants.k_cbMaxGameServerGameDescription];
	/// <summary>
	/// <para>/&lt; Steam App ID of this server</para>
	/// </summary>
	public uint m_nAppID;
	/// <summary>
	/// <para>/&lt; total number of players currently on the server.  INCLUDES BOTS!!</para>
	/// </summary>
	public int m_nPlayers;
	/// <summary>
	/// <para>/&lt; Maximum players that can join this server</para>
	/// </summary>
	public int m_nMaxPlayers;
	/// <summary>
	/// <para>/&lt; Number of bots (i.e simulated players) on this server</para>
	/// </summary>
	public int m_nBotPlayers;
	[MarshalAs(UnmanagedType.I1)]
	/// <summary>
	/// <para>/&lt; true if this server needs a password to join</para>
	/// </summary>
	public bool m_bPassword;
	[MarshalAs(UnmanagedType.I1)]
	/// <summary>
	/// <para>/&lt; Is this server protected by VAC</para>
	/// </summary>
	public bool m_bSecure;
	/// <summary>
	/// <para>/&lt; time (in unix time) when this server was last played on (for favorite/history servers)</para>
	/// </summary>
	public uint m_ulTimeLastPlayed;
	/// <summary>
	/// <para>/&lt; server version as reported to Steam</para>
	/// </summary>
	public int m_nServerVersion;

	/// <summary>
	/// <para>Game server name</para>
	/// </summary>
	private fixed byte m_szServerName[Constants.k_cbMaxGameServerName];

	/// <summary>
	/// <para>the tags this server exposes</para>
	/// </summary>
	private fixed byte m_szGameTags[Constants.k_cbMaxGameServerTags];

	/// <summary>
	/// <para>steamID of the game server - invalid if it's doesn't have one (old server, or not connected to Steam)</para>
	/// </summary>
	public CSteamID m_steamID;
}