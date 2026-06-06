using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;


//https://github.com/adamreeve/semver.net
//i just threw the whole thing in this single file, so we dont have to add an entire dll into the loader..
namespace SemanticVersioning
{
	/// <summary>
	/// A semantic version.
	/// </summary>
	public class Version : IComparable<Version>, IComparable, IEquatable<Version>
	{
		private readonly string _inputString;
		private readonly int _major;
		private readonly int _minor;
		private readonly int _patch;
		private readonly string _preRelease;
		private readonly string _build;

		/// <summary>
		/// The major component of the version.
		/// </summary>
		public int Major { get { return _major; } }

		/// <summary>
		/// The minor component of the version.
		/// </summary>
		public int Minor { get { return _minor; } }

		/// <summary>
		/// The patch component of the version.
		/// </summary>
		public int Patch { get { return _patch; } }

		/// <summary>
		/// The pre-release string, or null for no pre-release version.
		/// </summary>
		public string PreRelease { get { return _preRelease; } }

		/// <summary>
		/// The build string, or null for no build version.
		/// </summary>
		public string Build { get { return _build; } }

		/// <summary>
		/// Whether this version is a pre-release
		/// </summary>
		public bool IsPreRelease { get { return !string.IsNullOrEmpty(_preRelease); } }

		private static Regex strictRegex = new Regex(@"^
            ([0-9]|[1-9][0-9]+)       # major version
            \.
            ([0-9]|[1-9][0-9]+)       # minor version
            \.
            ([0-9]|[1-9][0-9]+)       # patch version
            (\-([0-9A-Za-z\-\.]+))?   # pre-release version
            (\+([0-9A-Za-z\-\.]+))?   # build metadata
            $",
			RegexOptions.IgnorePatternWhitespace);

		private static Regex looseRegex = new Regex(@"^
            [v=\s]*
            (\d+)                     # major version
            \.
            (\d+)                     # minor version
            \.
            (\d+)                     # patch version
            (\-?([0-9A-Za-z\-\.]+))?  # pre-release version
            (\+([0-9A-Za-z\-\.]+))?   # build metadata
            \s*
            $",
			RegexOptions.IgnorePatternWhitespace);

		/// <summary>
		/// Construct a new semantic version from a version string.
		/// </summary>
		/// <param name="input">The version string.</param>
		/// <param name="loose">When true, be more forgiving of some invalid version specifications.</param>
		/// <exception cref="System.ArgumentException">Thrown when the version string is invalid.</exception>
		public Version(string input, bool loose = false)
		{
			_inputString = input;

			var regex = loose ? looseRegex : strictRegex;

			var match = regex.Match(input);
			if (!match.Success)
			{
				throw new ArgumentException(String.Format("Invalid version string: {0}", input));
			}

			_major = Int32.Parse(match.Groups[1].Value);

			_minor = Int32.Parse(match.Groups[2].Value);

			_patch = Int32.Parse(match.Groups[3].Value);

			if (match.Groups[4].Success)
			{
				var inputPreRelease = match.Groups[5].Value;
				var cleanedPreRelease = PreReleaseVersion.Clean(inputPreRelease);
				if (!loose && inputPreRelease != cleanedPreRelease)
				{
					throw new ArgumentException(String.Format(
								"Invalid pre-release version: {0}", inputPreRelease));
				}
				_preRelease = cleanedPreRelease;
			}

			if (match.Groups[6].Success)
			{
				_build = match.Groups[7].Value;
			}
		}

		/// <summary>
		/// Construct a new semantic version from version components.
		/// </summary>
		/// <param name="major">The major component of the version.</param>
		/// <param name="minor">The minor component of the version.</param>
		/// <param name="patch">The patch component of the version.</param>
		/// <param name="preRelease">The pre-release version string, or null for no pre-release version.</param>
		/// <param name="build">The build version string, or null for no build version.</param>
		public Version(int major, int minor, int patch,
				string preRelease = null, string build = null)
		{
			_major = major;
			_minor = minor;
			_patch = patch;
			_preRelease = preRelease;
			_build = build;
		}

		/// <summary>
		/// Returns this version without any pre-release or build version.
		/// </summary>
		/// <returns>The base version</returns>
		public Version BaseVersion()
		{
			return new Version(Major, Minor, Patch);
		}

		/// <summary>
		/// Returns the original input string the version was constructed from or
		/// the cleaned version if the version was constructed from version components.
		/// </summary>
		/// <returns>The version string</returns>
		public override string ToString()
		{
			return _inputString ?? Clean();
		}

		/// <summary>
		/// Return a cleaned, normalised version string.
		/// </summary>
		/// <returns>The cleaned version string.</returns>
		public string Clean()
		{
			var preReleaseString = PreRelease == null ? ""
				: String.Format("-{0}", PreReleaseVersion.Clean(PreRelease));
			var buildString = Build == null ? ""
				: String.Format("+{0}", Build);

			return String.Format("{0}.{1}.{2}{3}{4}",
					Major, Minor, Patch, preReleaseString, buildString);
		}

		/// <summary>
		/// Calculate a hash code for the version.
		/// </summary>
		public override int GetHashCode()
		{
			// The build version isn't included when calculating the hash,
			// as two versions with equal properties except for the build
			// are considered equal.

			unchecked  // Allow integer overflow with wrapping
			{
				int hash = 17;
				hash = hash * 23 + Major.GetHashCode();
				hash = hash * 23 + Minor.GetHashCode();
				hash = hash * 23 + Patch.GetHashCode();
				if (PreRelease != null)
				{
					hash = hash * 23 + PreRelease.GetHashCode();
				}
				return hash;
			}
		}

		// Implement IEquatable<Version>
		/// <summary>
		/// Test whether two versions are semantically equivalent.
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool Equals(Version other)
		{
			if (ReferenceEquals(other, null))
			{
				return false;
			}
			return CompareTo(other) == 0;
		}

		// Implement IComparable
		public int CompareTo(object obj)
		{
			switch (obj)
			{
				case null:
				return 1;
				case Version v:
				return CompareTo(v);
				default:
				throw new ArgumentException("Object is not a Version");
			}
		}

		// Implement IComparable<Version>
		public int CompareTo(Version other)
		{
			if (ReferenceEquals(other, null))
			{
				return 1;
			}

			foreach (var c in PartComparisons(other))
			{
				if (c != 0)
				{
					return c;
				}
			}

			return PreReleaseVersion.Compare(this.PreRelease, other.PreRelease);
		}

		private IEnumerable<int> PartComparisons(Version other)
		{
			yield return Major.CompareTo(other.Major);
			yield return Minor.CompareTo(other.Minor);
			yield return Patch.CompareTo(other.Patch);
		}

		public override bool Equals(object other)
		{
			return Equals(other as Version);
		}

		// Static convenience methods

		/// <summary>
		/// Construct a new semantic version from a version string.
		/// </summary>
		/// <param name="input">The version string.</param>
		/// <param name="loose">When true, be more forgiving of some invalid version specifications.</param>
		/// <exception cref="System.ArgumentException">Thrown when the version string is invalid.</exception>
		/// <returns>The Version</returns>
		public static Version Parse(string input, bool loose = false)
			=> new Version(input, loose);

		/// <summary>
		/// Try to construct a new semantic version from a version string.
		/// </summary>
		/// <param name="input">The version string.</param>
		/// <param name="result">The Version, or null when parse fails.</param>
		/// <returns>A boolean indicating success of the parse operation.</returns>
		public static bool TryParse(string input, out Version result)
			=> TryParse(input, loose: false, out result);

		/// <summary>
		/// Try to construct a new semantic version from a version string.
		/// </summary>
		/// <param name="input">The version string.</param>
		/// <param name="loose">When true, be more forgiving of some invalid version specifications.</param>
		/// <param name="result">The Version, or null when parse fails.</param>
		/// <returns>A boolean indicating success of the parse operation.</returns>
		public static bool TryParse(string input, bool loose, out Version result)
		{
			try
			{
				result = Parse(input, loose);
				return true;
			}
			catch
			{
				result = null;
				return false;
			}
		}

		public static bool operator ==(Version a, Version b)
		{
			if (ReferenceEquals(a, null))
			{
				return ReferenceEquals(b, null);
			}
			return a.Equals(b);
		}

		public static bool operator !=(Version a, Version b)
		{
			return !(a == b);
		}

		public static bool operator >(Version a, Version b)
		{
			if (ReferenceEquals(a, null))
			{
				return false;
			}
			return a.CompareTo(b) > 0;
		}

		public static bool operator >=(Version a, Version b)
		{
			if (ReferenceEquals(a, null))
			{
				return ReferenceEquals(b, null) ? true : false;
			}
			return a.CompareTo(b) >= 0;
		}

		public static bool operator <(Version a, Version b)
		{
			if (ReferenceEquals(a, null))
			{
				return ReferenceEquals(b, null) ? false : true;
			}
			return a.CompareTo(b) < 0;
		}

		public static bool operator <=(Version a, Version b)
		{
			if (ReferenceEquals(a, null))
			{
				return true;
			}
			return a.CompareTo(b) <= 0;
		}
	}

	static internal class PreReleaseVersion
	{
		public static int Compare(string a, string b)
		{
			if (a == null && b == null)
			{
				return 0;
			}
			else if (a == null)
			{
				// No pre-release is > having a pre-release version
				return 1;
			}
			else if (b == null)
			{
				return -1;
			}
			else
			{
				foreach (var c in IdentifierComparisons(Identifiers(a), Identifiers(b)))
				{
					if (c != 0)
					{
						return c;
					}
				}
				return 0;
			}
		}

		public static string Clean(string input)
		{
			var identifierStrings = Identifiers(input).Select(i => i.Clean());
			return String.Join(".", identifierStrings.ToArray());
		}

		private class Identifier
		{
			public bool IsNumeric { get; set; }
			public int IntValue { get; set; }
			public string Value { get; set; }

			public Identifier(string input)
			{
				Value = input;
				SetNumeric();
			}

			public string Clean()
			{
				return IsNumeric ? IntValue.ToString() : Value;
			}

			private void SetNumeric()
			{
				int x;
				bool couldParse = Int32.TryParse(Value, out x);
				IsNumeric = couldParse && x >= 0;
				IntValue = x;
			}
		}

		private static IEnumerable<Identifier> Identifiers(string input)
		{
			foreach (var identifier in input.Split('.'))
			{
				yield return new Identifier(identifier);
			}
		}

		private static IEnumerable<int> IdentifierComparisons(
				IEnumerable<Identifier> aIdentifiers, IEnumerable<Identifier> bIdentifiers)
		{
			foreach (var identifiers in ZipIdentifiers(aIdentifiers, bIdentifiers))
			{
				var a = identifiers.Item1;
				var b = identifiers.Item2;
				if (a == b)
				{
					yield return 0;
				}
				else if (a == null)
				{
					yield return -1;
				}
				else if (b == null)
				{
					yield return 1;
				}
				else
				{
					if (a.IsNumeric && b.IsNumeric)
					{
						yield return a.IntValue.CompareTo(b.IntValue);
					}
					else if (!a.IsNumeric && !b.IsNumeric)
					{
						yield return String.CompareOrdinal(a.Value, b.Value);
					}
					else if (a.IsNumeric && !b.IsNumeric)
					{
						yield return -1;
					}
					else // !a.IsNumeric && b.IsNumeric
					{
						yield return 1;
					}
				}
			}
		}

		// Zip identifier sets until both have been exhausted, returning null
		// for identifier components not in one set.
		private static IEnumerable<Tuple<Identifier, Identifier>> ZipIdentifiers(
				IEnumerable<Identifier> first, IEnumerable<Identifier> second)
		{
			using (var ie1 = first.GetEnumerator())
			using (var ie2 = second.GetEnumerator())
			{
				while (ie1.MoveNext())
				{
					if (ie2.MoveNext())
					{
						yield return Tuple.Create(ie1.Current, ie2.Current);
					}
					else
					{
						yield return Tuple.Create<Identifier, Identifier>(ie1.Current, null);
					}
				}
				while (ie2.MoveNext())
				{
					yield return Tuple.Create<Identifier, Identifier>(null, ie2.Current);
				}
			}
		}
	}
}
