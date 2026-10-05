namespace SwiftlyS2.Shared.SteamAPI;

/// <summary>
/// <para>Used to return English-language diagnostic error messages to caller.</para>
/// <para>(For debugging or spewing to a console, etc.  Not intended for UI.)</para>
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential)]
public unsafe struct SteamNetworkingErrMsg
{
	public fixed byte m_SteamNetworkingErrMsg[Constants.k_cchMaxSteamNetworkingErrMsg];
}
