using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Lunaris.Config
{
	/// <summary>
	/// Handle returned by <see cref="IConfig.Register{T}"/>.
	/// </summary>
	public interface IConfigHandle<T> where T : class, new()
	{
		/// <summary> Get current config. </summary>
		/// <example><code>
		/// float val = config.Get().Val;
		/// </code></example>
		T Get();

		/// <summary>
		/// Subscribe to get notified of changes.
		/// </summary>
		/// <example><code>
		/// config.OnChanged(c =&gt; c.Val, val =&gt; { });
		/// </code></example>
		void OnChanged<TProp>(Expression<Func<T, TProp>> selector, Action<TProp> callback);

		/// <summary>
		/// Subscribe to get notified of changes.
		/// </summary>
		/// <example><code>
		/// config.OnChanged(config.Get().Val, val =&gt; { });
		/// </code></example>
		void OnChanged<TProp>(TProp selector, Action<TProp> callback, [CallerArgumentExpression(nameof(selector))] string expr = null);

		/// <summary>
		/// Subscribe to get notified on all changes.
		/// Returns full config.
		/// </summary>
		/// <example><code>
		/// config.OnAnyChanged += (cfg) =&gt; {};
		/// </code></example>
		event Action<T> OnAnyChanged;
	}
}