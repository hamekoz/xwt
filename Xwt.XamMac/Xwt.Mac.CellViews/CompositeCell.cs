// 
// CompositeCell.cs
//  
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
// 
// Copyright (c) 2011 Xamarin Inc
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
using System.Collections.Generic;
using System.Linq;
using AppKit;
using CoreGraphics;
using Foundation;
using ObjCRuntime;
using Xwt.Backends;

namespace Xwt.Mac
{
	class CompositeCell : NSView, ICopiableObject, ICellDataSource, INSCopying
	{
		ICellSource source;
		NSObject val;
		List<ICellRenderer> cells = new List<ICellRenderer> ();
		ITablePosition tablePosition;
		ApplicationContext context;

		public List<ICellRenderer> Cells {
			get {
				return cells;
			}
		}

		public CompositeCell (ApplicationContext context, ICellSource source)
		{
			if (source == null)
				throw new ArgumentNullException (nameof (source));
			this.context = context;
			this.source = source;
		}

		public CompositeCell (IntPtr p) : base (p)
		{
		}

		CompositeCell ()
		{
		}

		public ApplicationContext Context {
			get { return context; }
		}

		#region ICellDataSource implementation

		object ICellDataSource.GetValue (IDataField field)
		{
			return source.GetValue (tablePosition.Position, field.Index);
		}

		#endregion

		public void SetValue (IDataField field, object value)
		{
			source.SetValue (tablePosition.Position, field.Index, value);
		}

		public void SetCurrentEventRow ()
		{
			source.SetCurrentEventRow (tablePosition.Position);
		}

		public override NSObject Copy ()
		{
			var ob = (ICopiableObject)base.Copy ();
			ob.CopyFrom (this);
			return (NSObject)ob;
		}

		NSObject INSCopying.Copy (NSZone zone)
		{
			var ob = (ICopiableObject)new CompositeCell ();
			ob.CopyFrom (this);
			return (NSObject)ob;
		}

		void ICopiableObject.CopyFrom (object other)
		{
			var ob = (CompositeCell)other;
			if (ob.source == null)
				throw new ArgumentException ("Cannot copy from a CompositeCell with a null `source`");
			Identifier = ob.Identifier;
			context = ob.context;
			source = ob.source;
			cells = new List<ICellRenderer> ();
			foreach (var c in ob.cells) {
				var copy = (ICellRenderer)Activator.CreateInstance (c.GetType ());
				copy.CopyFrom (c);
				AddCell (copy);
			}
			if (tablePosition != null)
				Fill ();
		}

		public virtual NSObject ObjectValue {
			[Export ("objectValue")]
			get {
				return val;
			}
			[Export ("setObjectValue:")]
			set {
				val = value;
				if (val is ITablePosition) {
					tablePosition = (ITablePosition)val;
					Fill ();
				} else if (val is NSNumber) {
					tablePosition = new TableRow () {
						Row = ((NSNumber)val).Int32Value
					};
					Fill ();
				} else
					tablePosition = null;
			}
		}

		internal ITablePosition TablePosition {
			get { return tablePosition; }
		}

		public void AddCell (ICellRenderer cell)
		{
			cell.CellContainer = this;
			cells.Add (cell);
			AddSubview ((NSView)cell);
		}

		public void ClearCells ()
		{
			foreach (NSView cell in cells) {
				cell.RemoveFromSuperview ();
			}
			cells.Clear ();
		}

		public override CGRect Frame {
			get { return base.Frame; }
			set {
				var oldSize = base.Frame.Size;
				base.Frame = value;
				if (oldSize != value.Size && tablePosition != null) {
					foreach (var c in GetCells (new CGRect (CGPoint.Empty, value.Size))) {
						c.Cell.Frame = c.Frame;
						c.Cell.NeedsDisplay = true;
					}
				}
			}
		}

		public void Fill ()
		{
			foreach (var c in cells) {
				c.Backend.Load (c);
				c.Fill ();
			}

			foreach (var c in GetCells (new CGRect (CGPoint.Empty, Frame.Size))) {
				c.Cell.Frame = c.Frame;
				c.Cell.NeedsDisplay = true;
			}
		}

		IEnumerable<ICellRenderer> VisibleCells {
			get { return cells.Where (c => c.Backend.Frontend.Visible); }
		}

		public NSView GetCellViewForBackend (ICellViewBackend backend)
		{
			return cells.FirstOrDefault (c => c.Backend == backend) as NSView;
		}

		CGSize CalcSize ()
		{
			nfloat w = 0;
			nfloat h = 0;
			foreach (NSView c in VisibleCells) {
				var s = c.FittingSize;
				if (s.IsEmpty && SizeToFit (c))
					s = c.Frame.Size;
				w += s.Width;
				if (s.Height > h)
					h = s.Height;
			}
			return new CGSize (w, h);
		}

		public override CGSize FittingSize {
			get {
				return CalcSize ();
			}
		}

		static readonly Selector selSetBackgroundStyle = new Selector ("setBackgroundStyle:");

		NSBackgroundStyle backgroundStyle;

		public virtual NSBackgroundStyle BackgroundStyle {
			[Export ("backgroundStyle")]
			get {
				return backgroundStyle;
			}
			[Export ("setBackgroundStyle:")]
			set {
				backgroundStyle = value;
				foreach (NSView cell in cells)
					if (cell.RespondsToSelector (selSetBackgroundStyle)) {
						if (IntPtr.Size == 8)
							Messaging.void_objc_msgSend_Int64 (cell.Handle, selSetBackgroundStyle.Handle, (long)value);
						else
							Messaging.void_objc_msgSend_int (cell.Handle, selSetBackgroundStyle.Handle, (int)value);
					} else
						cell.NeedsDisplay = true;
			}
		}

		static readonly Selector sizeToFitSel = new Selector ("sizeToFit");

		protected virtual bool SizeToFit (NSView view)
		{
			if (view.RespondsToSelector (sizeToFitSel)) {
				Messaging.void_objc_msgSend (view.Handle, sizeToFitSel.Handle);
				return true;
			}
			return false;
		}
		
		IEnumerable<CellPos> GetCells (CGRect cellFrame)
		{
			int nexpands = 0;
			double requiredSize = 0;
			double availableSize = cellFrame.Width;

			var visibleCells = VisibleCells.ToArray ();
			var sizes = new double [visibleCells.Length];

			// Get the natural size of each child
			for (int i = 0; i < visibleCells.Length; i++) {
				var v = visibleCells[i] as NSView;
				var s = v.FittingSize;
				if (s.IsEmpty && SizeToFit (v))
					s = v.Frame.Size;
				sizes [i] = s.Width;
				requiredSize += s.Width;
				if (visibleCells [i].Backend.Frontend.Expands)
					nexpands++;
			}

			double remaining = availableSize - requiredSize;
			if (remaining > 0) {
				var expandRemaining = new SizeSplitter (remaining, nexpands);
				for (int i = 0; i < visibleCells.Length; i++) {
					if (visibleCells [i].Backend.Frontend.Expands)
						sizes [i] += (nfloat)expandRemaining.NextSizePart ();
				}
			}

			double x = cellFrame.X;
			for (int i = 0; i < visibleCells.Length; i++) {
				var cell = (NSView)visibleCells [i];
				var height = cell.FittingSize.Height;
				var y = (cellFrame.Height - height) / 2;
				yield return new CellPos { Cell = cell, Frame = new CGRect (x, y, sizes [i], height) };
				x += sizes [i];
			}
		}

		class CellPos
		{
			public NSView Cell;
			public CGRect Frame;
		}

		class SizeSplitter
		{
			int rem;
			int part;

			public SizeSplitter (double total, int numParts)
			{
				if (numParts > 0)
				{
					part = ((int)total) / numParts;
					rem = ((int)total) % numParts;
				}
			}

			public double NextSizePart ()
			{
				if (rem > 0)
				{
					rem--;
					return part + 1;
				} else
					return part;
			}
		}

		bool isDisposed;

		public bool IsDisposed {
			get {
				try {
					// Cocoa may dispose the native view in NSView based table mode
					// in this case Handle and SuperHandle will become Zero.
					return isDisposed || Handle == IntPtr.Zero || SuperHandle == IntPtr.Zero;
				} catch {
					return true;
				}
			}
		}

		protected override void Dispose(bool disposing)
		{
			isDisposed = true;
			base.Dispose(disposing);
		}
	}
}
