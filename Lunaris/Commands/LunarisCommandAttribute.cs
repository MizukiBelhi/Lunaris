using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris
{

	/// <summary>
	/// Marks a method as a Command, automatically registering it with the command system
	/// and making it visible in the plugin installer.
	/// All registered plugin commands can also be seen by typing <c>/lunaris help</c>.
	/// </summary>
	/// <remarks>
	/// The command is registered in chat as <c>/pluginname commandname</c>, where <c>pluginname</c> comes from the <see cref="LunarisPluginAttribute"/> name (spaces removed and converted to lowercase).
	/// The method's parameters determine the command's arguments, and only the following types are supported:
	/// <list type="bullet">
	///   <item><c>string</c></item>
	///   <item><c>int</c></item>
	///   <item><c>float</c></item>
	/// </list>
	/// 
	/// > [!WARNING]
	/// > The method must match a supported parameter signature (0-8 args) and params.
	/// > - Using unsupported types will throw an <see cref="System.ArgumentException"/>.
	/// > - Using more than 8 parameters will throw a <see cref="System.NotSupportedException"/>.
	/// </remarks>
	/// <example><code>
	/// [LunarisCommand("heal", "Heals the player by the specified amount")]
	/// public void HealCommand(float amount)
	/// {
	///     player.Heal(amount);
	/// }
	///
	/// // In chat: /myplugin heal 50
	/// </code></example>
	/// <exception cref="System.ArgumentException">Thrown if a parameter type is not one of the supported types (<c>string</c>, <c>int</c>, <c>float</c>).</exception>
	/// <exception cref="System.NotSupportedException">Thrown if the method has more than 8 parameters.</exception>
	[AttributeUsage(AttributeTargets.Method)]
	public sealed class LunarisCommandAttribute : Attribute
	{
		public string Name { get; }
		public string Description { get; }

		/// <exclude/>
		public LunarisCommandAttribute(string name, string description)
		{
			Name = name;
			Description = description;
		}
	}
}
