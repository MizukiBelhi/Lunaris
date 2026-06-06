using UnityEngine;
using Lunaris.Message;
using Lunaris.IPC;
using Lunaris.Config;

namespace Lunaris
{


	public abstract class LunarisPlugin : MonoBehaviour
	{

		/// <summary>
		/// Logging.
		/// </summary>
		public ILog Logging;


		/// <summary>
		/// Configuration.
		/// </summary>
		public IConfig Config;


		/// <summary>
		/// Popup Notifications.
		/// </summary>
		public INotifications Notification;

		/// <summary>
		/// Override and use this method for ImGui drawing.
		/// </summary>
		///  <remarks>
		///  > [!NOTE]
		///  > Using ImGui in any other method will not work.
		///  </remarks>
		public virtual void OnImGuiDraw() { }

		/// <summary>
		/// Creates a named IPC channel that this plugin exposes to other plugins.
		/// Use this to register functions or actions that other plugins can invoke or subscribe to.
		/// </summary>
		/// <typeparam name="TRet"> The return type of the exposed function. Use <c>object</c> if unused. </typeparam>
		/// <param name="name"> Unique name for this channel, e.g. <c>"MyPlugin.GetPlayerHealth"</c>. </param>
		/// <example><code>
		/// // PluginA exposes a function
		/// var provider = IPCAuraProvider&lt;float&gt;("MyPlugin.GetHealth");
		/// provider.RegisterFunc(() => player.health);
		///
		/// // PluginA broadcasts to all subscribers
		/// provider.SendMessage();
		/// </code></example>
		public IAuraProvider<TRet> IPCAuraProvider<TRet>(string name) => new Aura<TRet>(name, this);

		/// <summary>
		/// Creates a named IPC channel that this plugin exposes to other plugins, with one input parameter.
		/// </summary>
		/// <typeparam name="T1"> The type of the first parameter. </typeparam>
		/// <typeparam name="TRet"> The return type of the exposed function. Use <c>object</c> if unused. </typeparam>
		/// <param name="name"> Unique name for this channel. </param>
		/// <example><code>
		/// var provider = IPCAuraProvider&lt;string, bool&gt;("MyPlugin.IsEnemy");
		/// provider.RegisterFunc(name => enemies.Contains(name));
		/// provider.SendMessage("Goblin");
		/// </code></example>
		public IAuraProvider<T1, TRet> IPCAuraProvider<T1, TRet>(string name) 
			=> new Aura<T1, TRet>(name, this);

		/// <summary> Creates a named IPC provider channel with 2 input parameters. </summary>
		/// <inheritdoc cref="IPCAuraProvider{T1, TRet}"/>
		public IAuraProvider<T1, T2, TRet> IPCAuraProvider<T1, T2, TRet>(string name)
			=> new Aura<T1, T2, TRet>(name, this);

		/// <summary> Creates a named IPC provider channel with 3 input parameters. </summary>
		/// <inheritdoc cref="IPCAuraProvider{T1, TRet}"/>
		public IAuraProvider<T1, T2, T3, TRet> IPCAuraProvider<T1, T2, T3, TRet>(string name)
			=> new Aura<T1, T2, T3, TRet>(name, this);

		/// <summary> Creates a named IPC provider channel with 4 input parameters. </summary>
		/// <inheritdoc cref="IPCAuraProvider{T1, TRet}"/>
		public IAuraProvider<T1, T2, T3, T4, TRet> IPCAuraProvider<T1, T2, T3, T4, TRet>(string name)
			=> new Aura<T1, T2, T3, T4, TRet>(name, this);

		/// <summary> Creates a named IPC provider channel with 5 input parameters. </summary>
		/// <inheritdoc cref="IPCAuraProvider{T1, TRet}"/>
		public IAuraProvider<T1, T2, T3, T4, T5, TRet> IPCAuraProvider<T1, T2, T3, T4, T5, TRet>(string name)
			=> new Aura<T1, T2, T3, T4, T5, TRet>(name, this);

		/// <summary> Creates a named IPC provider channel with 6 input parameters. </summary>
		/// <inheritdoc cref="IPCAuraProvider{T1, TRet}"/>
		public IAuraProvider<T1, T2, T3, T4, T5, T6, TRet> IPCAuraProvider<T1, T2, T3, T4, T5, T6, TRet>(string name)
			=> new Aura<T1, T2, T3, T4, T5, T6, TRet>(name, this);

		/// <summary> Creates a named IPC provider channel with 7 input parameters. </summary>
		/// <inheritdoc cref="IPCAuraProvider{T1, TRet}"/>
		public IAuraProvider<T1, T2, T3, T4, T5, T6, T7, TRet> IPCAuraProvider<T1, T2, T3, T4, T5, T6, T7, TRet>(string name)
			=> new Aura<T1, T2, T3, T4, T5, T6, T7, TRet>(name, this);

		/// <summary> Creates a named IPC provider channel with 8 input parameters. </summary>
		/// <inheritdoc cref="IPCAuraProvider{T1, TRet}"/>
		public IAuraProvider<T1, T2, T3, T4, T5, T6, T7, T8, TRet> IPCAuraProvider<T1, T2, T3, T4, T5, T6, T7, T8, TRet>(string name)
			=> new Aura<T1, T2, T3, T4, T5, T6, T7, T8, TRet>(name, this);

		/// <summary>
		/// Subscribes to a named IPC channel exposed by another plugin.
		/// Use this to listen for broadcasts or invoke another plugin's registered handler.
		/// </summary>
		/// <typeparam name="TRet"> The return type of the remote function. Use <c>object</c> if unused. </typeparam>
		/// <param name="name"> The channel name to subscribe to, e.g. <c>"OtherPlugin.GetPlayerHealth"</c>. </param>
		/// <example><code>
		/// // PluginB subscribes to PluginA's channel
		/// var sub = IPCAuraSubscriber&lt;float&gt;("MyPlugin.GetHealth");
		/// sub.Subscribe(() => UpdateHealthBar());
		///
		/// // Or invoke directly and get the return value
		/// float hp = sub.InvokeFunc();
		/// </code></example>
		public IAuraSubscriber<TRet> IPCAuraSubscriber<TRet>(string name)
			=> new Aura<TRet>(name, this);

		/// <summary> Subscribes to a named IPC channel with 1 input parameter. </summary>
		/// <inheritdoc cref="IPCAuraSubscriber{TRet}"/>
		public IAuraSubscriber<T1, TRet> IPCAuraSubscriber<T1, TRet>(string name)
			=> new Aura<T1, TRet>(name, this);

		/// <summary> Subscribes to a named IPC channel with 2 input parameters. </summary>
		/// <inheritdoc cref="IPCAuraSubscriber{TRet}"/>
		public IAuraSubscriber<T1, T2, TRet> IPCAuraSubscriber<T1, T2, TRet>(string name)
			=> new Aura<T1, T2, TRet>(name, this);

		/// <summary> Subscribes to a named IPC channel with 3 input parameters. </summary>
		/// <inheritdoc cref="IPCAuraSubscriber{TRet}"/>
		public IAuraSubscriber<T1, T2, T3, TRet> IPCAuraSubscriber<T1, T2, T3, TRet>(string name)
			=> new Aura<T1, T2, T3, TRet>(name, this);

		/// <summary> Subscribes to a named IPC channel with 4 input parameters. </summary>
		/// <inheritdoc cref="IPCAuraSubscriber{TRet}"/>
		public IAuraSubscriber<T1, T2, T3, T4, TRet> IPCAuraSubscriber<T1, T2, T3, T4, TRet>(string name)
			=> new Aura<T1, T2, T3, T4, TRet>(name, this);

		/// <summary> Subscribes to a named IPC channel with 5 input parameters. </summary>
		/// <inheritdoc cref="IPCAuraSubscriber{TRet}"/>
		public IAuraSubscriber<T1, T2, T3, T4, T5, TRet> IPCAuraSubscriber<T1, T2, T3, T4, T5, TRet>(string name)
			=> new Aura<T1, T2, T3, T4, T5, TRet>(name, this);

		/// <summary> Subscribes to a named IPC channel with 6 input parameters. </summary>
		/// <inheritdoc cref="IPCAuraSubscriber{TRet}"/>
		public IAuraSubscriber<T1, T2, T3, T4, T5, T6, TRet> IPCAuraSubscriber<T1, T2, T3, T4, T5, T6, TRet>(string name)
			=> new Aura<T1, T2, T3, T4, T5, T6, TRet>(name, this);

		/// <summary> Subscribes to a named IPC channel with 7 input parameters. </summary>
		/// <inheritdoc cref="IPCAuraSubscriber{TRet}"/>
		public IAuraSubscriber<T1, T2, T3, T4, T5, T6, T7, TRet> IPCAuraSubscriber<T1, T2, T3, T4, T5, T6, T7, TRet>(string name)
			=> new Aura<T1, T2, T3, T4, T5, T6, T7, TRet>(name, this);

		/// <summary> Subscribes to a named IPC channel with 8 input parameters. </summary>
		/// <inheritdoc cref="IPCAuraSubscriber{TRet}"/>
		public IAuraSubscriber<T1, T2, T3, T4, T5, T6, T7, T8, TRet> IPCAuraSubscriber<T1, T2, T3, T4, T5, T6, T7, T8, TRet>(string name)
			=> new Aura<T1, T2, T3, T4, T5, T6, T7, T8, TRet>(name, this);

		/// <exclude/>
		protected LunarisPlugin()
		{
			PluginInitializer.InstantiateFields(this, GetType().Assembly);
		}
	}

}
