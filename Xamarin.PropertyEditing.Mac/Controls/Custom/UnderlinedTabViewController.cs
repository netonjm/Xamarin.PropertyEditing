﻿using System.Linq;
using AppKit;

namespace Xamarin.PropertyEditing.Mac
{
	internal class UnderlinedTabViewController<TViewModel> : NotifyingTabViewController<TViewModel> where TViewModel : NotifyingObject
	{
		public override void NumberOfItemsChanged (NSTabView tabView)
		{
			base.NumberOfItemsChanged (tabView);
			var items = this.tabStack.Views.ToList ();
			foreach (var view in items) {
				this.tabStack.RemoveView (view);
				view.Dispose ();
			}
			var i = 0;
			foreach (var item in TabViewItems) {
				if (item.Image != null) {
					this.tabStack.AddView (new UnderlinedImageView (item.Image.Name) {
						Selected = i == SelectedTabViewItemIndex,
						ToolTip = item.ToolTip,
					}, NSStackViewGravity.Leading);
				} else {
					this.tabStack.AddView (new UnderlinedTextField () {
						BackgroundColor = NSColor.Clear,
						Editable = false,
						Bezeled = false,
						StringValue = item.Label,
						Selected = i == SelectedTabViewItemIndex,
						ToolTip = item.ToolTip,
					}, NSStackViewGravity.Leading);
				}
				i++;
			}
		}

		private NSStackView outerStack;
		private NSStackView innerStack;
		protected NSStackView tabStack = new NSStackView () {
			Spacing = 10f,
		};

		private NSEdgeInsets edgeInsets = new NSEdgeInsets (0, 0, 0, 0);
		public NSEdgeInsets EdgeInsets {
			get => this.edgeInsets;
			set {
				this.edgeInsets = value;
				if (this.outerStack != null) {
					this.outerStack.EdgeInsets = value;
					this.innerStack.EdgeInsets = value;
				}
			}
		}

		public override void MouseDown (NSEvent theEvent)
		{
			NSView hit = View.HitTest (View.Superview.ConvertPointFromView (theEvent.LocationInWindow, null));
			if (!(hit is IUnderliningTabView))
				return;

			int i = 0;
			foreach (var label in tabStack.Views) {
				if (hit == label) {
					SelectedTabViewItemIndex = i;
					break;
				}
				i++;
			}
		}

		public override void DidSelect (NSTabView tabView, NSTabViewItem item)
		{
			base.DidSelect (tabView, item);
			for (int i = 0; i < this.tabStack.Views.Length; i++) {
				var tabItem = this.tabStack.Views[i] as IUnderliningTabView;
				if (tabItem != null)
					tabItem.Selected = SelectedTabViewItemIndex == i;
			}
		}

		public override void LoadView ()
		{
			this.outerStack = new NSStackView () {
				Orientation = NSUserInterfaceLayoutOrientation.Horizontal,
				Spacing = 0,
				EdgeInsets = new NSEdgeInsets (0, edgeInsets.Left, 0, edgeInsets.Right)
			};

			this.innerStack = new NSStackView () {
				Spacing = 0,
				Alignment = NSLayoutAttribute.Left,
				Orientation = NSUserInterfaceLayoutOrientation.Vertical,
				EdgeInsets = new NSEdgeInsets (edgeInsets.Top, 0, edgeInsets.Bottom, 0)
			};

			this.outerStack.AddView (this.innerStack, NSStackViewGravity.Leading);
			this.innerStack.AddView (this.tabStack, NSStackViewGravity.Leading);
			this.innerStack.AddView (TabView, NSStackViewGravity.Bottom);
			View = this.outerStack;
		}
	}
}
