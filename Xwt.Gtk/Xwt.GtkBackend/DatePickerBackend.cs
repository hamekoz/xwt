//
// DatePickerBackend.cs
//
// Author:
//       Jérémie Laval <jeremie.laval@xamarin.com>
//
// Copyright (c) 2012 Xamarin, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Linq;
using System.Collections.Generic;
using Xwt.Backends;


namespace Xwt.GtkBackend
{
	public class DatePickerBackend : WidgetBackend, IDatePickerBackend
	{
		public override void Initialize ()
		{
			Widget = new GtkDatePicker ();
			Widget.ValueChanged += HandleValueChanged;
			Widget.ShowAll ();
		}

		new GtkDatePicker Widget {
			get { return (GtkDatePicker)base.Widget; }
			set { base.Widget = value; }
		}

		protected new IDatePickerEventSink EventSink {
			get { return (IDatePickerEventSink)base.EventSink; }
		}

		public DateTime DateTime {
			get {
				return Widget.CurrentValue;
			}
			set {
				Widget.CurrentValue = value;
			}
		}

		public DatePickerStyle Style {
			get {
				return Widget.Style;
			}
			set {
				Widget.Style = value;
			}
		}

		void HandleValueChanged (object sender, EventArgs e)
		{
			ApplicationContext.InvokeUserCode (delegate {
				EventSink.ValueChanged ();
			});
		}

		public class GtkDatePicker : Gtk.HBox
		{
			GtkDatePickerEntry datepickerentry = new GtkDatePickerEntry ();
			Xwt.ToggleButton toggleButton = new ToggleButton () {
				Image = StockIcons.Calendar.WithSize (12),
				ImagePosition = ContentPosition.Center,
			};
			Xwt.Calendar calendar = new Calendar ();
			Xwt.SpinButton hours = new SpinButton () {
				MinimumValue = 0,
				MaximumValue = 24,
				IncrementValue = 1,
				Digits = 0,
				TooltipText = "HH",
			};
			Xwt.SpinButton minutes = new SpinButton () {
				MinimumValue = 0,
				MaximumValue = 59,
				IncrementValue = 1,
				Digits = 0,
				TooltipText = "mm"
			};
			Xwt.SpinButton seconds = new SpinButton () {
				MinimumValue = 0,
				MaximumValue = 59,
				IncrementValue = 1,
				Digits = 0,
				TooltipText = "ss",
			};
			Xwt.Popover popover = new Popover ();
			Xwt.VBox datetimeBox = new VBox ();
			Xwt.HBox timeBox = new HBox ();

			public GtkDatePicker ()
			{
				toggleButton.HorizontalPlacement = WidgetPlacement.Start;
				CurrentValue = DateTime.Now;
//				calendar.Date = DateTime.Now.Date;
//				hours.Value = (double)DateTime.Now.Hour;
//				minutes.Value = (double)DateTime.Now.Minute;
//				seconds.Value = (double)DateTime.Now.Second;
				toggleButton.HeightRequest = (double)datepickerentry.HeightRequest;
				toggleButton.WidthRequest = (double)datepickerentry.HeightRequest;
//				datepickerentry.Changed += delegate(object sender, EventArgs e) {
//					if (!toggleButton.Visible) {
//						calendar.Date = datepickerentry.CurrentValue.Date;
//						hours.Value = (double)datepickerentry.CurrentValue.Hour;
//						minutes.Value = (double)datepickerentry.CurrentValue.Minute;
//						seconds.Value = (double)datepickerentry.CurrentValue.Second;
//						HandleValueChanged (sender, e);
//					}
//				};

				calendar.ButtonReleased += (object sender, ButtonEventArgs e) => {
					if (e.MultiplePress >= 2)
						popover.Hide ();
				};

				timeBox.PackStart (hours);
				timeBox.PackStart (minutes);
				timeBox.PackStart (seconds);

				datetimeBox.PackStart (calendar);
				datetimeBox.PackStart (timeBox);

				popover.Content = datetimeBox;
				popover.Closed += delegate {
					toggleButton.Active = false;
				};
				toggleButton.Toggled += delegate {
					if (toggleButton.Active) {
						calendar.Date = datepickerentry.CurrentValue.Date;
						hours.Value = (double)datepickerentry.CurrentValue.Hour;
						minutes.Value = (double)datepickerentry.CurrentValue.Minute;
						seconds.Value = (double)datepickerentry.CurrentValue.Second;
						popover.Show (Popover.Position.Top, toggleButton);
					} else {
						var datetime = new DateTime (
							               calendar.Date.Year,
							               calendar.Date.Month,
							               calendar.Date.Day,
							               (int)hours.Value,
							               (int)minutes.Value,
							               (int)seconds.Value
						               );
						datepickerentry.CurrentValue = datetime;
						popover.Hide ();
					}
				};
				Add (datepickerentry);
				var nativeToggleButton = (Gtk.ToggleButton)Xwt.Toolkit.CurrentEngine.GetNativeWidget (toggleButton);
				PackEnd (nativeToggleButton, false, false, 0);
			}

			public DateTime CurrentValue {
				get {
					return datepickerentry.CurrentValue;
				}
				set {
					datepickerentry.CurrentValue = value;
				}
			}

			DatePickerStyle style = DatePickerStyle.DateTime;

			public DatePickerStyle Style {
				get {
					return style;
				}
				set {
					style = value;
					switch (style) {
					case DatePickerStyle.Date:
						datetimeBox.Visible = false;
						timeBox.Visible = false;
						break;
					case DatePickerStyle.DateTime:
						datetimeBox.Visible = true;
						timeBox.Visible = true;
						break;
					case DatePickerStyle.Time:
						datetimeBox.Visible = false;
						timeBox.Visible = true;
						break;
					}
				}
			}

			public EventHandler ValueChanged;

			void HandleValueChanged (object sender, EventArgs e)
			{
				if (ValueChanged != null)
					ValueChanged (this, e);
			}
		}

		public class GtkDatePickerEntry : Gtk.SpinButton
		{
			enum Component
			{
				None = 0,
				Month,
				Day,
				Year,
				Hour,
				Minute,
				Second
			}

			public new EventHandler ValueChanged;

			// We use the format of the invariant culture which is american biased apparently
			const string DateTimeFormat = "MM/dd/yyyy HH:mm:ss";
			Component selectedComponent;
			double oldValue = -1;
			int internalChangeCntd;

			int startPos = -1, endPos = -1;
			int currentDigitInsert;

			public GtkDatePickerEntry () : base (DateTime.MinValue.Ticks,
				                                    DateTime.MaxValue.Ticks,
				                                    TimeSpan.TicksPerSecond)
			{
				Adjustment.PageIncrement = TimeSpan.TicksPerDay;
				IsEditable = true;
				HasFrame = false;
				CurrentValue = DateTime.Now;
				Adjustment.ValueChanged += HandleValueChanged;
			}

			// Hack to supply the right tick value
			void HandleValueChanged (object sender, EventArgs e)
			{
				// Prevent reentrant call
				if (internalChangeCntd > 0) {
					internalChangeCntd--;
					return;
				}
				double currentValue = oldValue;

				var adjustedValue = Adjustment.Value;
				if (adjustedValue > currentValue)
					GoUp (ref currentValue);
				else if (adjustedValue < currentValue)
					GoDown (ref currentValue);

				internalChangeCntd++;
				Adjustment.Value = currentValue;
				RaiseChangedEvent ();

				oldValue = Adjustment.Value;
			}

			protected override int OnOutput ()
			{
				DateTime dateTime = CurrentValue;
				Text = dateTime.ToString (DateTimeFormat);

				return 1;
			}

			protected override int OnInput (out double newValue)
			{
				newValue = Adjustment.Value;
				return 1;
			}

			void GoDown (ref double newValue)
			{
				switch (selectedComponent) {
				case Component.Second:
					newValue -= TimeSpan.TicksPerSecond;
					break;
				case Component.Minute:
					newValue -= TimeSpan.TicksPerMinute;
					break;
				case Component.Hour:
					newValue -= TimeSpan.TicksPerHour;
					break;
				case Component.Day:
					newValue = CurrentValue.AddDays (-1).Ticks;
					break;
				case Component.Month:
					newValue = CurrentValue.AddMonths (-1).Ticks;
					break;
				case Component.Year:
					newValue = CurrentValue.AddYears (-1).Ticks;
					break;
				}
			}

			void GoUp (ref double newValue)
			{
				switch (selectedComponent) {
				case Component.Second:
					newValue += TimeSpan.TicksPerSecond;
					break;
				case Component.Minute:
					newValue += TimeSpan.TicksPerMinute;
					break;
				case Component.Hour:
					newValue += TimeSpan.TicksPerHour;
					break;
				case Component.Day:
					newValue = CurrentValue.AddDays (1).Ticks;
					break;
				case Component.Month:
					newValue = CurrentValue.AddMonths (1).Ticks;
					break;
				case Component.Year:
					newValue = CurrentValue.AddYears (1).Ticks;
					break;
				}
			}

			protected override bool OnButtonPressEvent (Gdk.EventButton evnt)
			{
				int posX = (int)evnt.X, posY = (int)evnt.Y;
				//GetPointer (out posX, out posY);
				int layoutX, layoutY;
				GetLayoutOffsets (out layoutX, out layoutY);
				int index, trailing;

				bool result = Layout.XyToIndex (Pango.Units.FromPixels (posX - layoutX),
					              Pango.Units.FromPixels (posY - layoutY),
					              out index,
					              out trailing);

				// Hacky. Since it's entry that maintain the text GdkWindow it's normally always
				// the last children of the widget GdkWindow because the other children are created
				// by the spin button
				if (evnt.Window == GdkWindow.Children.Last () && result) {
					index = TextIndexToLayoutIndex (index);
					UpdateSelectedComponent (index);
					currentDigitInsert = 0;
					GrabFocus ();
					return false;
				} else {
					return base.OnButtonPressEvent (evnt);
				}
			}

			// Override default GTK behavior which is to select the whole entry
			protected override void OnFocusGrabbed ()
			{
				base.OnFocusGrabbed ();
				SelectRegion (startPos, endPos);
			}

			void UpdateSelectedComponent (int index)
			{
				int componentSelected = -1;
				string txt = Text;
				List<int> stops = Enumerable.Range (0, txt.Length)
					.Where (i => char.IsPunctuation (txt, i) || char.IsSeparator (txt, i))
					.ToList ();
				stops.Insert (0, -1);
				stops.Add (txt.Length);

				for (int i = 0; i < stops.Count - 1 && stops [i] <= index; i++) {
					componentSelected = i;
					startPos = stops [i] + 1;
					endPos = stops [i + 1];
				}
				selectedComponent = (Component)componentSelected + 1;
			}

			protected override void OnChanged ()
			{
				base.OnChanged ();
				if (startPos == -1 || endPos == -1)
					SelectRegion (0, 0);
				else
					SelectRegion (startPos, endPos);
			}

			protected override bool OnKeyReleaseEvent (Gdk.EventKey evnt)
			{
				char pressedKey = (char)Gdk.Keyval.ToUnicode (evnt.KeyValue);
				if (char.IsDigit (pressedKey) && selectedComponent != Component.None && pressedKey > '0') {
					try {
						int value = (int)pressedKey - (int)'0';
						DateTime current = CurrentValue;
						switch (selectedComponent) {
						case Component.Month:
							current = FromCopy (current, month: ValueFromDigitInsert (current.Month, value));
							break;
						case Component.Day:
							current = FromCopy (current, day: ValueFromDigitInsert (current.Day, value));
							break;
						case Component.Year:
							current = FromCopy (current, year: ValueFromDigitInsert (current.Year, value));
							break;
						case Component.Hour:
							current = FromCopy (current, hour: ValueFromDigitInsert (current.Hour, value));
							break;
						case Component.Minute:
							current = FromCopy (current, minute: ValueFromDigitInsert (current.Minute, value));
							break;
						case Component.Second:
							current = FromCopy (current, second: ValueFromDigitInsert (current.Second, value));
							break;
						}
						currentDigitInsert++;
						CurrentValue = current;
					} catch (ArgumentOutOfRangeException) {
						// In case date wasn't representable we redo the call with an updated digit insert
						currentDigitInsert = 0;
						return OnKeyReleaseEvent (evnt);
					}
				}
				return base.OnKeyReleaseEvent (evnt);
			}

			protected override bool OnKeyPressEvent (Gdk.EventKey evnt)
			{
				// We only allow the keypress to proceed to the normal handler
				// if it doesn't involve adding an actual character
				// (i.e. navigation keys)
				uint value = Gdk.Keyval.ToUnicode (evnt.KeyValue);
				if (value == 0)
					return base.OnKeyPressEvent (evnt);
				else
					return true;
			}

			int ValueFromDigitInsert (int baseValue, int newValue)
			{
				return currentDigitInsert == 0 ? newValue : newValue + baseValue * 10;
			}

			DateTime FromCopy (DateTime source,
			                   int year = -1,
			                   int month = -1,
			                   int day = -1,
			                   int hour = -1,
			                   int minute = -1,
			                   int second = -1)
			{
				return new DateTime (year == -1 ? source.Year : year,
					month == -1 ? source.Month : month,
					day == -1 ? source.Day : day,
					hour == -1 ? source.Hour : hour,
					minute == -1 ? source.Minute : minute,
					second == -1 ? source.Second : second);
			}

			void RaiseChangedEvent ()
			{
				//var tmp = ValueChanged;
				if (ValueChanged != null)
					ValueChanged (this, EventArgs.Empty);
			}

			public DateTime CurrentValue {
				get {
					return new DateTime ((long)Adjustment.Value);
				}
				set {
					// Inhibit our custom handler for specific set
					internalChangeCntd++;
					Adjustment.Value = value.Ticks;
					oldValue = value.Ticks;
					RaiseChangedEvent ();
				}
			}
		}
	}
}

