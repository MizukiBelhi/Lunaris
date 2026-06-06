using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UI;
using UnityEngine;
using System.Diagnostics;

namespace Lunaris
{
	internal class TitleScreenMenu
	{
		private enum State
		{
			Hide,
			Show,
			FadeOut,
		}

		private static State state = State.Hide;

		public static List<TitleScreenMenuEntry> entries = [];

		static Sprite shadeTexture = null;
		private static bool isInit = false;

		private static InOutCubic fadeOutEasing;
		private static readonly Dictionary<Guid, InOutCubic> shadeEasings = [];
		private static readonly Dictionary<Guid, InOutQuint> moveEasings = [];
		private static readonly Dictionary<Guid, InOutCubic> logoEasings = [];


		public static void Draw()
		{
			if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Menu") return;
			if (!isInit)
			{
				shadeTexture = Bridge.tsmShade;
				isInit = true;
			}

			Rect windowRect = new Rect(0, 800, 300, Screen.height - 200);
			bool hovered = windowRect.Contains(Event.current.mousePosition);
			UnityEngine.GUI.BeginGroup(windowRect);

			switch (state)
			{
				case State.Show:
				{
					var i = 0;
					bool allDone = true;

					foreach (var entry in entries)
					{
						if (!moveEasings.TryGetValue(entry.Id, out var moveEasing))
						{
							moveEasing = new InOutQuint(TimeSpan.FromMilliseconds(400));
							moveEasings.Add(entry.Id, moveEasing);
						}

						if (!moveEasing.IsRunning && !moveEasing.IsDone) moveEasing.Restart();
						moveEasing.Update();
						if (!moveEasing.IsDone) allDone = false;

						var rowHeight = shadeTexture.texture.height + 8;
						var finalPos = (i) * rowHeight;

						var easedValue = Mathf.Clamp01((float)moveEasing.Value);
						var yPos = easedValue * finalPos;

						if (moveEasing.IsDone)
						{
							yPos = finalPos;
							moveEasing.Stop();
						}

						Rect entryRect = new Rect(0, yPos, 300, rowHeight-8);
						DrawEntry(entryRect, entry, moveEasing.IsRunning && i != 0, true, i == 0, true, moveEasing.IsDone);

						i++;
					}

					if (allDone && !hovered) state = State.FadeOut;
					break;
				}

				case State.FadeOut:
				{
					if(fadeOutEasing == null)
						fadeOutEasing = new InOutCubic(TimeSpan.FromMilliseconds(400)) { IsInverse = true };
					if (!fadeOutEasing.IsRunning && !fadeOutEasing.IsDone) fadeOutEasing.Restart();
					fadeOutEasing.Update();

					Color oldColor = GUI.color;
					GUI.color = new Color(1, 1, 1, Mathf.Clamp01((float)fadeOutEasing.Value));
					var alph = Mathf.Clamp01((float)fadeOutEasing.Value);
					var i = 0;
					foreach (var entry in entries)
					{
						var finalPos = (i) * (shadeTexture.texture.height + 8);
						Rect entryRect = new Rect(0, finalPos, 300, shadeTexture.texture.height);
						DrawEntry(entryRect, entry, i != 0, true, i == 0, false, false, alph, i==0);
						i++;
					}

					GUI.color = oldColor;

					if (fadeOutEasing.IsDone)
					{
						state = hovered ? State.Show : State.Hide;
						fadeOutEasing = null;
					}
					break;
				}

				case State.Hide:
				{
					Rect triggerRect = new Rect(0, 0, 300, shadeTexture.texture.height);
					if (entries.Count > 0 && DrawEntry(triggerRect, entries[0], true, false, true, true, false))
					{
						state = State.Show;
					}

					moveEasings.Clear();
					logoEasings.Clear();
					shadeEasings.Clear();
					break;
				}
			}

			GUI.EndGroup();
		}

		private static bool DrawEntry(Rect rect, TitleScreenMenuEntry entry, bool inhibitFadeout, bool showText, bool isFirst, bool overrideAlpha, bool interactable, float forceAlpha=0f, bool igfirst=false)
		{
			if (!shadeEasings.TryGetValue(entry.Id, out var shadeEasing))
			{
				shadeEasing = new InOutCubic(TimeSpan.FromMilliseconds(350));
				shadeEasings.Add(entry.Id, shadeEasing);
			}

			bool isHover = rect.Contains(Event.current.mousePosition);

			if (isHover && (!shadeEasing.IsRunning || (shadeEasing.IsDone && shadeEasing.IsInverse)) && !inhibitFadeout)
			{
				shadeEasing.IsInverse = false;
				shadeEasing.Restart();
			}
			else if (!isHover && !shadeEasing.IsInverse && shadeEasing.IsRunning && !inhibitFadeout)
			{
				shadeEasing.IsInverse = true;
				shadeEasing.Restart();
			}
			shadeEasing.Update();

			if (isHover && Event.current.type == EventType.MouseDown && interactable)
			{
				entry.Callback?.Invoke();
				Event.current.Use();
			}

			if (!logoEasings.TryGetValue(entry.Id, out var logoEasing))
			{
				logoEasing = new InOutCubic(TimeSpan.FromMilliseconds(350));
				logoEasings.Add(entry.Id, logoEasing);
			}

			if (!logoEasing.IsRunning && !logoEasing.IsDone) logoEasing.Restart();
			if (logoEasing.IsDone) logoEasing.Stop();
			logoEasing.Update();


			Color oldCol = GUI.color;
			//if(!overrideAlpha)
				GUI.color = new Color(1, 1, 1, (float)shadeEasing.Value);
			GUI.DrawTexture(new Rect(rect.x, rect.y+4, shadeTexture.texture.width / 2f, shadeTexture.texture.height), shadeTexture.texture);
			GUI.color = oldCol;
			if (forceAlpha != 0)
				GUI.color = new Color(1, 1, 1, forceAlpha);

			float contentAlpha = 1f;
			if (overrideAlpha) contentAlpha = isFirst ? 1f : (float)logoEasing.Value;
			else if (isFirst) contentAlpha = 1f;

			Rect iconRect = new Rect(rect.x + 5, rect.y + 5, 64, 64);
			if(!overrideAlpha)
				GUI.color = new Color(1, 1, 1, contentAlpha);
			if (forceAlpha != 0 && !igfirst)
				GUI.color = new Color(1, 1, 1, forceAlpha);
			GUI.DrawTexture(iconRect, entry.Texture.texture);

			if (showText || !overrideAlpha)
			{
				GUIStyle textStyle = new GUIStyle(GUI.skin.label);
				textStyle.fontSize = 14;
				textStyle.normal.textColor = Color.white;

				float textHeight = textStyle.CalcHeight(new GUIContent(entry.Name), 200);
				float textY = rect.y + (64 / 2f) - (textHeight / 2f);
				Rect textRect = new Rect(iconRect.xMax + 10, textY, 200, textHeight);

				float textAlpha = overrideAlpha ? (showText ? (float)logoEasing.Value : 0f) : contentAlpha;
				if (!overrideAlpha)
					GUI.color = new Color(0, 0, 0, textAlpha);
				if (forceAlpha != 0)
					GUI.color = new Color(1, 1, 1, forceAlpha);
				GUI.Label(new Rect(textRect.x, textRect.y + 1, textRect.width, textRect.height), entry.Name, textStyle);
				if (!overrideAlpha)
					GUI.color = new Color(1, 1, 1, textAlpha);
				if (forceAlpha != 0)
					GUI.color = new Color(1, 1, 1, forceAlpha);
				GUI.Label(textRect, entry.Name, textStyle);
			}

			GUI.color = oldCol;
			if (forceAlpha != 0)
				GUI.color = new Color(1, 1, 1, forceAlpha);
			return isHover;
		}
	}


	internal class TitleScreenMenuEntry
	{
		public string Name;
		public Sprite Texture;
		public Action Callback;
		public Guid Id = Guid.NewGuid();

		public TitleScreenMenuEntry(string _Name, Sprite _Texture, Action _Callback)
		{
			Name = _Name;
			Texture = _Texture;
			Callback = _Callback;
		}
	}



	internal class InOutCubic : Easing
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="InOutCubic"/> class.
		/// </summary>
		/// <param name="duration">The duration of the animation.</param>
		public InOutCubic(TimeSpan duration)
			: base(duration)
		{
			// ignored
		}

		/// <inheritdoc/>
		public override void Update()
		{
			var p = this.Progress;
			this.Value = p < 0.5 ? 4 * p * p * p : 1 - (Math.Pow((-2 * p) + 2, 3) / 2);
		}
	}


	internal class InOutQuint : Easing
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="InOutQuint"/> class.
		/// </summary>
		/// <param name="duration">The duration of the animation.</param>
		public InOutQuint(TimeSpan duration)
			: base(duration)
		{
			// ignored
		}

		/// <inheritdoc/>
		public override void Update()
		{
			var p = this.Progress;
			this.Value = p < 0.5 ? 16 * p * p * p * p * p : 1 - (Math.Pow((-2 * p) + 2, 5) / 2);
		}
	}


	internal abstract class Easing
	{
		public static float Lerp(float firstFloat, float secondFloat, float by)
		{
			return (firstFloat * (1 - @by)) + (secondFloat * by);
		}

		/// <summary>
		/// Lerp between two vectors.
		/// </summary>
		/// <param name="firstVector">The first vector.</param>
		/// <param name="secondVector">The second float.</param>
		/// <param name="by">The point to lerp to.</param>
		/// <returns>The lerped vector.</returns>
		public static Vector2 Lerp(Vector2 firstVector, Vector2 secondVector, float by)
		{
			var retX = Lerp(firstVector.x, secondVector.x, by);
			var retY = Lerp(firstVector.y, secondVector.y, by);
			return new Vector2(retX, retY);
		}

		// TODO: Use game delta time here instead
		private readonly Stopwatch animationTimer = new Stopwatch();

		private double valueInternal;

		/// <summary>
		/// Initializes a new instance of the <see cref="Easing"/> class with the specified duration.
		/// </summary>
		/// <param name="duration">The animation duration.</param>
		protected Easing(TimeSpan duration)
		{
			this.Duration = duration;
		}

		/// <summary>
		/// Gets or sets the origin point of the animation.
		/// </summary>
		public Vector2? Point1 { get; set; }

		/// <summary>
		/// Gets or sets the destination point of the animation.
		/// </summary>
		public Vector2? Point2 { get; set; }

		/// <summary>
		/// Gets the resulting point at the current timestep.
		/// </summary>
		public Vector2 EasedPoint { get; private set; }

		/// <summary>
		/// Gets or sets a value indicating whether the result of the easing should be inversed.
		/// </summary>
		public bool IsInverse { get; set; }

		/// <summary>
		/// Gets or sets the current value of the animation, from 0 to 1.
		/// </summary>
		public double Value
		{
			get
			{
				if (this.IsInverse)
					return 1 - this.valueInternal;

				return this.valueInternal;
			}

			protected set
			{
				this.valueInternal = value;

				if (this.Point1.HasValue && this.Point2.HasValue)
					this.EasedPoint = Lerp(this.Point1.Value, this.Point2.Value, (float)this.valueInternal);
			}
		}

		/// <summary>
		/// Gets or sets the duration of the animation.
		/// </summary>
		public TimeSpan Duration { get; set; }

		/// <summary>
		/// Gets a value indicating whether or not the animation is running.
		/// </summary>
		public bool IsRunning => this.animationTimer.IsRunning;

		/// <summary>
		/// Gets a value indicating whether or not the animation is done.
		/// </summary>
		public bool IsDone => this.animationTimer.ElapsedMilliseconds > this.Duration.TotalMilliseconds;

		/// <summary>
		/// Gets the progress of the animation, from 0 to 1.
		/// </summary>
		protected double Progress => this.animationTimer.ElapsedMilliseconds / this.Duration.TotalMilliseconds;

		/// <summary>
		/// Starts the animation from where it was last stopped, or from the start if it was never started before.
		/// </summary>
		public void Start()
		{
			this.animationTimer.Start();
		}

		/// <summary>
		/// Stops the animation at the current point.
		/// </summary>
		public void Stop()
		{
			this.animationTimer.Stop();
		}

		/// <summary>
		/// Restarts the animation.
		/// </summary>
		public void Restart()
		{
			this.animationTimer.Restart();
		}

		/// <summary>
		/// Resets the animation.
		/// </summary>
		public void Reset()
		{
			this.animationTimer.Reset();
		}

		/// <summary>
		/// Updates the animation.
		/// </summary>
		public abstract void Update();
	}
}
