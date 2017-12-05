﻿using UnityEngine;
using System.Collections;
using LiteNetLib;
using System.Collections.Generic;
using LiteNetLib.Utils;
using TinyBirdUtils;
using TinyBirdNet.Messaging;
using System;

namespace TinyBirdNet {

	public abstract class TinyNetScene : System.Object, INetEventListener {

		public virtual string TYPE { get { return "Abstract"; } }

		//protected static Dictionary<string, GameObject> guidToPrefab;

		public static Action<TinyNetConnection, int> createPlayerAction;

		/// <summary>
		/// int is the NetworkID of the TinyNetIdentity object.
		/// </summary>
		protected static Dictionary<int, TinyNetIdentity> _localIdentityObjects = new Dictionary<int, TinyNetIdentity>();

		/// <summary>
		/// int is the NetworkID of the ITinyNetObject object.
		/// </summary>
		protected static Dictionary<int, ITinyNetObject> _localNetObjects = new Dictionary<int, ITinyNetObject>();

		/// <summary>
		/// If using this, always Reset before use!
		/// </summary>
		protected static NetDataWriter recycleWriter = new NetDataWriter();

		protected static TinyNetMessageReader recycleMessageReader = new TinyNetMessageReader();

		// static message objects to avoid runtime-allocations
		protected static TinyNetObjectHideMessage s_TinyNetObjectHideMessage = new TinyNetObjectHideMessage();
		protected static TinyNetObjectDestroyMessage s_TinyNetObjectDestroyMessage = new TinyNetObjectDestroyMessage();
		protected static TinyNetObjectSpawnMessage s_TinyNetObjectSpawnMessage = new TinyNetObjectSpawnMessage();
		protected static TinyNetObjectSpawnSceneMessage s_TinyNetObjectSpawnSceneMessage = new TinyNetObjectSpawnSceneMessage();
		protected static TinyNetObjectSpawnFinishedMessage s_TineNetObjectSpawnFinishedMessage = new TinyNetObjectSpawnFinishedMessage();
		protected static TinyNetAddPlayerMessage s_TinyNetAddPlayerMessage = new TinyNetAddPlayerMessage();
		protected static TinyNetRemovePlayerMessage s_TinyNetRemovePlayerMessage = new TinyNetRemovePlayerMessage();
		protected static TinyNetRequestAddPlayerMessage s_TinyNetRequestAddPlayerMessage = new TinyNetRequestAddPlayerMessage();
		protected static TinyNetRequestRemovePlayerMessage s_TinyNetRequestRemovePlayerMessage = new TinyNetRequestRemovePlayerMessage();
		protected static TinyNetClientAuthorityMessage s_TinyNetClientAuthorityMessage = new TinyNetClientAuthorityMessage();

		protected TinyNetMessageHandlers _tinyMessageHandlers = new TinyNetMessageHandlers();

		protected List<TinyNetConnection> _tinyNetConns;
		public List<TinyNetConnection> tinyNetConns { get { return _tinyNetConns; } }

		public TinyNetConnection connToHost { get; protected set; }

		protected NetManager _netManager;

		protected int currentFixedFrame = 0;

		/// <summary>
		/// Returns true if socket listening and update thread is running.
		/// </summary>
		public bool isRunning { get {
				if (_netManager == null) {
					return false;
				}

				return _netManager.IsRunning;
		} }

		/// <summary>
		/// Returns true if it's connected to at least one peer.
		/// </summary>
		public bool isConnected {
			get {
				if (_netManager == null) {
					return false;
				}

				return _netManager.PeersCount > 0;
			}
		}

		public TinyNetScene() {
			_tinyNetConns = new List<TinyNetConnection>(TinyNetGameManager.instance.MaxNumberOfPlayers);

			/*if (guidToPrefab == null) {
				guidToPrefab = TinyNetGameManager.instance.GetDictionaryOfAssetGUIDToPrefabs();
			}*/
		}

		protected virtual void RegisterMessageHandlers() {
			//RegisterHandlerSafe(MsgType.Rpc, OnRPCMessage);
			//RegisterHandlerSafe(MsgType.SyncEvent, OnSyncEventMessage);
			//RegisterHandlerSafe(MsgType.AnimationTrigger, NetworkAnimator.OnAnimationTriggerClientMessage);
		}

		public void RegisterHandler(ushort msgType, TinyNetMessageDelegate handler) {
			_tinyMessageHandlers.RegisterHandler(msgType, handler);
		}

		public void RegisterHandlerSafe(ushort msgType, TinyNetMessageDelegate handler) {
			_tinyMessageHandlers.RegisterHandlerSafe(msgType, handler);
		}

		/// <summary>
		/// It is called from TinyNetGameManager Update(), handles PollEvents().
		/// </summary>
		public virtual void InternalUpdate() {
			if (_netManager != null) {
				_netManager.PollEvents();
			}
		}

		public virtual void TinyNetUpdate() {
		}

		public virtual void ClearNetManager() {
			if (_netManager != null) {
				_netManager.Stop();
			}
		}

		protected virtual void ConfigureNetManager(bool bUseFixedTime) {
			if (bUseFixedTime) {
				_netManager.UpdateTime = Mathf.FloorToInt(Time.fixedDeltaTime * 1000);
			} else {
				_netManager.UpdateTime = 15;
			}

			_netManager.PingInterval = TinyNetGameManager.instance.PingInterval;
			_netManager.NatPunchEnabled = TinyNetGameManager.instance.bNatPunchEnabled;

			RegisterMessageHandlers();
		}

		public virtual void ToggleNatPunching(bool bNewState) {
			_netManager.NatPunchEnabled = bNewState;
		}

		public virtual void SetPingInterval(int newPingInterval) {
			if (_netManager != null) {
				_netManager.PingInterval = newPingInterval;
			}
		}

		protected virtual TinyNetConnection CreateTinyNetConnection(NetPeer peer) {
			//No default implemention
			return null;
		}

		protected TinyNetConnection GetTinyNetConnection(NetPeer peer) {
			foreach (TinyNetConnection tinyNetCon in tinyNetConns) {
				if (tinyNetCon.netPeer == peer) {
					return tinyNetCon;
				}
			}

			return null;
		}

		protected virtual bool RemoveTinyNetConnection(NetPeer peer) {
			foreach (TinyNetConnection tinyNetCon in tinyNetConns) {
				if (tinyNetCon.netPeer == peer) {
					tinyNetConns.Remove(tinyNetCon);
					return true;
				}
			}

			return false;
		}

		protected virtual bool RemoveTinyNetConnection(long connectId) {
			foreach (TinyNetConnection tinyNetCon in tinyNetConns) {
				if (tinyNetCon.ConnectId == connectId) {
					tinyNetConns.Remove(tinyNetCon);
					return true;
				}
			}

			return false;
		}

		//============ Object Networking ====================//

		public static void AddTinyNetIdentityToList(TinyNetIdentity netIdentity) {
			_localIdentityObjects.Add(netIdentity.NetworkID, netIdentity);
		}

		public static void AddTinyNetObjectToList(ITinyNetObject netObj) {
			_localNetObjects.Add(netObj.NetworkID, netObj);
		}

		public static void RemoveTinyNetIdentityFromList(TinyNetIdentity netIdentity) {
			_localIdentityObjects.Remove(netIdentity.NetworkID);
		}

		public static void RemoveTinyNetObjectFromList(ITinyNetObject netObj) {
			_localNetObjects.Remove(netObj.NetworkID);
		}

		public static TinyNetIdentity GetTinyNetIdentityByNetworkID(int nId) {
			return _localIdentityObjects[nId];
		}

		public static ITinyNetObject GetTinyNetObjectByNetworkID(int nId) {
			return _localNetObjects[nId];
		}

		//============ TinyNetMessages Networking ===========//

		ushort ReadMessageAndCallDelegate(NetDataReader reader, NetPeer peer) {
			ushort msgType = reader.GetUShort();

			if (_tinyMessageHandlers.Contains(msgType)) {
				recycleMessageReader.msgType = msgType;
				recycleMessageReader.reader = reader;
				recycleMessageReader.tinyNetConn = GetTinyNetConnection(peer);
				recycleMessageReader.channelId = SendOptions.ReliableOrdered; //@TODO: I currently don't know if it's possible to get from which channel a message came.

				_tinyMessageHandlers.GetHandler(msgType)(recycleMessageReader);
			}

			return msgType;
		}

		public virtual void SendMessageByChannelToTargetConnection(ITinyNetMessage msg, SendOptions sendOptions, TinyNetConnection tinyNetConn) {
			recycleWriter.Reset();

			recycleWriter.Put(msg.msgType);
			msg.Serialize(recycleWriter);

			tinyNetConn.Send(recycleWriter, sendOptions);
		}

		public virtual void SendMessageByChannelToAllConnections(ITinyNetMessage msg, SendOptions sendOptions) {
			recycleWriter.Reset();

			recycleWriter.Put(msg.msgType);
			msg.Serialize(recycleWriter);
			
			for (int i = 0; i < tinyNetConns.Count; i++) {
				tinyNetConns[i].Send(recycleWriter, sendOptions);
			}
		}

		public virtual void SendMessageByChannelToAllReadyConnections(ITinyNetMessage msg, SendOptions sendOptions) {
			recycleWriter.Reset();

			recycleWriter.Put(msg.msgType);
			msg.Serialize(recycleWriter);

			for (int i = 0; i < tinyNetConns.Count; i++) {
				if (!tinyNetConns[i].isReady) {
					return;
				}
				tinyNetConns[i].Send(recycleWriter, sendOptions);
			}
		}

		public virtual void SendMessageByChannelToAllObserversOf(TinyNetIdentity tni, ITinyNetMessage msg, SendOptions sendOptions) {
			recycleWriter.Reset();

			recycleWriter.Put(msg.msgType);
			msg.Serialize(recycleWriter);

			for (int i = 0; i < tinyNetConns.Count; i++) {
				if (!tinyNetConns[i].IsObservingNetIdentity(tni)) {
					return;
				}
				tinyNetConns[i].Send(recycleWriter, sendOptions);
			}
		}

		//============ INetEventListener methods ============//

		public virtual void OnPeerConnected(NetPeer peer) {
			TinyLogger.Log("[" + TYPE + "] We have new peer: " + peer.EndPoint + " connectId: " + peer.ConnectId);

			CreateTinyNetConnection(peer);
		}

		public virtual void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
			TinyLogger.Log("[" + TYPE + "] disconnected from: " + peer.EndPoint + " because " + disconnectInfo.Reason);

			RemoveTinyNetConnection(peer);
		}

		public virtual void OnNetworkError(NetEndPoint endPoint, int socketErrorCode) {
			TinyLogger.Log("[" + TYPE + "] error " + socketErrorCode + " at: " + endPoint);
		}

		public void OnNetworkReceive(NetPeer peer, NetDataReader reader) {
			TinyLogger.Log("[" + TYPE + "] received message " + TinyNetMsgType.MsgTypeToString(ReadMessageAndCallDelegate(reader, peer)) + " from: " + peer.EndPoint);
		}

		public virtual void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType) {
			TinyLogger.Log("[" + TYPE + "] Received Unconnected message from: " + remoteEndPoint);

			if (messageType == UnconnectedMessageType.DiscoveryRequest) {
				OnDiscoveryRequestReceived(remoteEndPoint, reader);
			}
		}

		public virtual void OnNetworkLatencyUpdate(NetPeer peer, int latency) {
			TinyLogger.Log("[" + TYPE + "] Latency update for peer: " + peer.EndPoint + " " + latency + "ms");
		}

		/*public virtual void OnNetworkReceive(NetPeer peer, NetDataReader reader) {
			TinyLogger.Log("[" + TYPE + "] On network receive from: " + peer.EndPoint);
		}*/

		//============ Network Events =======================//

		protected virtual void OnDiscoveryRequestReceived(NetEndPoint remoteEndPoint, NetDataReader reader) {
			TinyLogger.Log("[" + TYPE + "] Received discovery request. Send discovery response");
			_netManager.SendDiscoveryResponse(new byte[] { 1 }, remoteEndPoint);
		}

		//============ Players Methods ======================//

		protected virtual void AddPlayerControllerToConnection(TinyNetConnection conn, int playerControllerId) {
			if (playerControllerId < 0) {
				if (TinyNetLogLevel.logError) { TinyLogger.LogError("AddPlayerControllerToConnection() called with playerControllerId < 0"); }
				return;
			}

			if (playerControllerId < conn.playerControllers.Count && conn.playerControllers[playerControllerId].IsValid) {
				if (TinyNetLogLevel.logError) { TinyLogger.LogError("There is already a player with that playerControllerId for this connection"); }
				return;
			}

			CreatePlayerAndAdd(conn, playerControllerId);
		}

		protected virtual void RemovePlayerControllerFromConnection(TinyNetConnection conn, short playerControllerId) {
			conn.RemovePlayerController(playerControllerId);
		}

		protected virtual void CreatePlayerAndAdd(TinyNetConnection conn, int playerControllerId) {
			if (createPlayerAction != null) {
				createPlayerAction(conn, playerControllerId);
				return;
			}
			// If no action is set, we just use default implementation
			conn.SetPlayerController<TinyNetPlayerController>(new TinyNetPlayerController((short)playerControllerId));
		}
	}
}
