using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AppKit;
using Foundation;
using Xamarin.PropertyEditing.ViewModels;

namespace Xamarin.PropertyEditing.Mac
{
	internal class AutocompleteTextField : NSTextField
	{
		private const string AutocompleteItemsString = "AutocompleteItems";
		private const string PreviewCustomExpressionString = "PreviewCustomExpression";

		private IReadOnlyList<string> autocompleteValues;
		private PropertyInfo previewCustomExpressionInfo;
		private PropertyInfo customcompleteItemsPropertyInfo;

		private PropertyViewModel viewModel;
		public PropertyViewModel ViewModel {
			get { 
				return this.viewModel; 
			}
			internal set {
				this.viewModel = value;

				if (this.viewModel != null) {
					var vmType = ViewModel.GetType ();
					if (this.customcompleteItemsPropertyInfo == null)
						this.customcompleteItemsPropertyInfo = vmType.GetProperty (AutocompleteItemsString);

					if (this.previewCustomExpressionInfo == null)
						this.previewCustomExpressionInfo = vmType.GetProperty (PreviewCustomExpressionString);
				}
			}
		}

		public AutocompleteTextField ()
		{
			var actDelegate = new AutocompleteTextFieldDelegate ();
			actDelegate.TextChanged += (object sender, string e) => {
				this.previewCustomExpressionInfo.SetValue (this.viewModel, e);
			};

			Delegate = actDelegate;
		}

		internal string[] Suggestions ()
		{
			var values = this.customcompleteItemsPropertyInfo.GetValue (this.viewModel);
			if (values != null) {
				if (this.autocompleteValues == null)
					this.autocompleteValues = ((IReadOnlyList<string>)values);

				return autocompleteValues.ToArray ();
			} else
				return null;
		}

		internal class AutocompleteTextFieldDelegate : NSTextFieldDelegate
		{
			public event EventHandler<string> TextChanged;

			public override void Changed (NSNotification notification)
			{
				var fieldEditor = notification.UserInfo.ObjectForKey (new NSString ("NSFieldEditor")) as NSTextView;
				TextChanged?.Invoke (this, fieldEditor?.Value);

				fieldEditor?.Complete (null);
			}

			public override string[] GetCompletions (NSControl control, NSTextView textView, string[] words, NSRange charRange, ref nint index)
			{
				var actf = control as AutocompleteTextField;
				var suggestions = actf?.Suggestions ();
				index = suggestions != null ? suggestions.ToList ().FindIndex (str => str.Contains (control.StringValue, StringComparison.OrdinalIgnoreCase)) : -1;
				return suggestions;
			}
		}
	}
}
