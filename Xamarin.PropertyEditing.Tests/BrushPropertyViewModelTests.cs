using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using Xamarin.PropertyEditing.Drawing;
using Xamarin.PropertyEditing.ViewModels;

namespace Xamarin.PropertyEditing.Tests
{
	internal abstract class BrushPropertyViewModelTests : PropertyViewModelTests<CommonBrush, PropertyViewModel<CommonBrush>>
	{
		protected override PropertyViewModel<CommonBrush> GetViewModel (TargetPlatform platform, IPropertyInfo property, IEnumerable<IObjectEditor> editors)
		{
			return new BrushPropertyViewModel (platform, property, editors);
		}

		[Test]
		public void SettingValueTriggersOpacityChange()
		{
			var mockProperty = new Mock<IPropertyInfo> ();
			mockProperty.SetupGet (pi => pi.Type).Returns (typeof (CommonBrush));
			var mockEditor = new MockObjectEditor (mockProperty.Object);

			var vm = new BrushPropertyViewModel (MockEditorProvider.MockPlatform, mockProperty.Object, new[] { mockEditor });
			var changed = false;
			vm.PropertyChanged += (s, e) => {
				if (e.PropertyName == nameof(BrushPropertyViewModel.Opacity)) {
					changed = true;
				}
			};
			vm.Value = GetRandomTestValue();
			Assert.IsTrue (changed);
		}

		[Test]
		public void ChangingEditorsUpdatesResources ()
		{
			var mockProperty = new Mock<IPropertyInfo> ();
			mockProperty.SetupGet (pi => pi.Type).Returns (typeof (CommonBrush));
			var mockEditor = new MockObjectEditor (mockProperty.Object);

			var vm = new BrushPropertyViewModel (new TargetPlatform (new MockEditorProvider(), new MockResourceProvider()), mockProperty.Object, new [] { mockEditor });

			var changed = false;
			vm.PropertyChanged += (s, e) => {
				if (e.PropertyName == nameof (BrushPropertyViewModel.ResourceSelector)) {
					changed = true;
				}
			};
			var rs1 = vm.ResourceSelector;
			vm.Editors.Add (new MockObjectEditor ());
			var rs2 = vm.ResourceSelector;
			Assert.IsTrue (changed);
			Assert.AreNotEqual (rs1, rs2);
		}
	}
}
