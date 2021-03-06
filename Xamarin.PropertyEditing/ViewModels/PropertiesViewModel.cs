using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cadenza.Collections;
using Xamarin.PropertyEditing.Drawing;

namespace Xamarin.PropertyEditing.ViewModels
{
	internal abstract class PropertiesViewModel
		: NotifyingObject, INotifyDataErrorInfo
	{
		public PropertiesViewModel (TargetPlatform targetPlatform)
		{
			if (targetPlatform == null)
				throw new ArgumentNullException (nameof(targetPlatform));

			TargetPlatform = targetPlatform;

			this.selectedObjects.CollectionChanged += OnSelectedObjectsChanged;
		}

		public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

		/// <remarks>Consumers should check for <see cref="INotifyCollectionChanged"/> and hook appropriately.</remarks>
		public IReadOnlyList<EditorViewModel> Properties => this.editors;

		public IReadOnlyList<EventViewModel> Events => this.events;

		public ICollection<object> SelectedObjects => this.selectedObjects;

		public string TypeName
		{
			get { return this.typeName; }
			private set
			{
				if (this.typeName == value)
					return;

				this.typeName = value;
				OnPropertyChanged ();
			}
		}

		public bool IsObjectNameable => this.nameable != null;

		public bool IsObjectNameReadOnly
		{
			get { return this.nameReadOnly; }
			private set
			{
				if (this.nameReadOnly == value)
					return;

				this.nameReadOnly = value;
				OnPropertyChanged ();
			}
		}

		public string ObjectName
		{
			get { return this.objectName; }
			set
			{
				if (this.objectName == value)
					return;

				SetObjectName (value);
			}
		}

		public bool EventsEnabled
		{
			get { return this.eventsEnabled; }
			private set
			{
				if (this.eventsEnabled == value)
					return;

				this.eventsEnabled = value;
				OnPropertyChanged ();
			}
		}

		public TargetPlatform TargetPlatform
		{
			get;
		}

		public bool HasErrors => this.errors.IsValueCreated && this.errors.Value.Count > 0;

		public IEnumerable GetErrors (string propertyName)
		{
			if (!this.errors.IsValueCreated)
				return Enumerable.Empty<string> ();

			string error;
			if (this.errors.Value.TryGetValue (propertyName, out error))
				return new[] { error };

			return Enumerable.Empty<string> ();
		}

		public PropertyViewModel<T> GetKnownPropertyViewModel<T> (KnownProperty<T> property)
		{
			if (property == null)
				throw new ArgumentNullException (nameof (property));
			if (this.knownEditors == null)
				throw new InvalidOperationException ("Querying for known properties before they've been setup");
			if (!this.knownEditors.TryGetValue (property, out EditorViewModel model))
				throw new KeyNotFoundException ();

			var vm = model as PropertyViewModel<T>;
			if (vm == null)
				throw new InvalidOperationException ("KnownProperty doesn't return expected property view model type");

			return vm;
		}

		protected IReadOnlyList<IObjectEditor> ObjectEditors => this.objEditors;

		/// <param name="newError">The error message or <c>null</c> to clear the error.</param>
		protected void SetError (string property, string newError)
		{
			if (this.errors.IsValueCreated) {
				string prevError;
				if (this.errors.Value.TryGetValue (property, out prevError)) {
					if (prevError == newError)
						return;
				}
			}

			if (newError == null)
				this.errors.Value.Remove (property);
			else
				this.errors.Value[property] = newError;

			OnErrorsChanged (new DataErrorsChangedEventArgs (property));
		}

		// TODO: Consider having the property hooks at the top level and a map of IPropertyInfo -> PropertyViewModel
		// the hash lookup would be likely faster than doing a property info compare in every property and would
		// reduce the number event attach/detatches

		protected virtual async void OnSelectedObjectsChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			var tcs = new TaskCompletionSource<bool> ();
			var existingTask = Interlocked.Exchange (ref this.busyTask, tcs.Task);
			if (existingTask != null)
				await existingTask;

			IObjectEditor[] newEditors = null;
			IObjectEditor[] removedEditors = null;

			switch (e.Action) {
				case NotifyCollectionChangedAction.Add: {
					newEditors = await AddEditorsAsync (e.NewItems);
					break;
				}

				case NotifyCollectionChangedAction.Remove:
					removedEditors = new IObjectEditor[e.OldItems.Count];
					for (int i = 0; i < e.OldItems.Count; i++) {
						IObjectEditor editor = this.objEditors.First (oe => oe?.Target == e.OldItems[i]);
						INotifyCollectionChanged notifier = editor.Properties as INotifyCollectionChanged;
						if (notifier != null)
							notifier.CollectionChanged -= OnObjectEditorPropertiesChanged;

						removedEditors[i] = editor;
						this.objEditors.Remove (editor);
					}
					break;

				case NotifyCollectionChangedAction.Replace:
				case NotifyCollectionChangedAction.Move:
				case NotifyCollectionChangedAction.Reset: {
					removedEditors = new IObjectEditor[this.objEditors.Count];
					for (int i = 0; i < removedEditors.Length; i++) {
						removedEditors[i] = this.objEditors[i];
						INotifyCollectionChanged notifier = removedEditors[i]?.Properties as INotifyCollectionChanged;
						if (notifier != null)
							notifier.CollectionChanged -= OnObjectEditorPropertiesChanged;
					}

					this.objEditors.Clear ();

					newEditors = await AddEditorsAsync (this.selectedObjects);
					break;
				}
			}

			UpdateMembers (removedEditors, newEditors);
			tcs.SetResult (true);
		}

		protected virtual void OnAddEditors (IEnumerable<EditorViewModel> editors)
		{
		}

		protected virtual void OnRemoveEditors (IEnumerable<EditorViewModel> editors)
		{
		}

		protected virtual void OnClearProperties()
		{
		}

		private INameableObject nameable;
		private bool nameReadOnly;
		private bool eventsEnabled;
		private string typeName, objectName;
		private BidirectionalDictionary<KnownProperty, EditorViewModel> knownEditors;
		private readonly List<IObjectEditor> objEditors = new List<IObjectEditor> ();
		private readonly ObservableCollectionEx<EditorViewModel> editors = new ObservableCollectionEx<EditorViewModel> ();
		private readonly ObservableCollectionEx<object> selectedObjects = new ObservableCollectionEx<object> ();
		private readonly ObservableCollectionEx<EventViewModel> events = new ObservableCollectionEx<EventViewModel> (); 
		private readonly Lazy<Dictionary<string, string>> errors = new Lazy<Dictionary<string, string>>();

		private void OnErrorsChanged (DataErrorsChangedEventArgs e)
		{
			ErrorsChanged?.Invoke (this, e);
		}

		private void AddProperties (IEnumerable<EditorViewModel> newEditors)
		{
			if (this.knownEditors != null) {
				// Only properties common across obj editors will be listed, so knowns should also be common
				var knownProperties = newEditors.First ().Editors.First().KnownProperties;
				if (knownProperties != null && knownProperties.Count > 0) {
					foreach (var editorvm in newEditors) {
						var prop = editorvm as PropertyViewModel;
						if (prop == null)
							continue;

						if (knownProperties.TryGetValue (prop.Property, out KnownProperty known)) {
							this.knownEditors[known] = editorvm;
						}
					}
				}
			}

			this.editors.AddRange (newEditors);
			OnAddEditors (newEditors);
		}

		private void RemoveProperties (IEnumerable<EditorViewModel> oldEditors)
		{
			if (this.knownEditors != null) {
				foreach (EditorViewModel old in oldEditors) {
					this.knownEditors.Inverse.Remove (old);
				}
			}

			this.editors.RemoveRange (oldEditors);
			OnRemoveEditors (oldEditors);
		}

		private async void SetObjectName (string value)
		{
			if (this.nameable == null)
				return;

			try {
				await this.nameable.SetNameAsync (value);
			} catch (Exception ex) {
				AggregateException aggregate = ex as AggregateException;
				if (aggregate != null) {
					aggregate = aggregate.Flatten ();
					ex = aggregate.InnerExceptions[0];
				}

				SetError (nameof(ObjectName), ex.ToString());
			} finally {
				SetCurrentObjectName (value, isReadonly: false);
			}
		}

		private void SetNameable (INameableObject nameable)
		{
			this.nameable = nameable;
			OnPropertyChanged (nameof (IsObjectNameable));
		}

		private void SetCurrentObjectName (string value, bool isReadonly)
		{
			IsObjectNameReadOnly = isReadonly;
			this.objectName = value;
			OnPropertyChanged (nameof (ObjectName));
		}

		private void ClearMembers()
		{
			TypeName = null;
			SetNameable (null);
			SetCurrentObjectName (null, isReadonly: true);
			this.editors.Clear ();
			this.events.Clear ();
			OnClearProperties ();
		}

		private void UpdateMembers (IObjectEditor[] removedEditors = null, IObjectEditor[] newEditors = null)
		{
			if (this.objEditors.Count == 0) {
				ClearMembers ();
				return;
			}

			IObjectEditor editor = this.objEditors[0];

			Task<string> nameQuery = null;
			INameableObject firstNameable = editor as INameableObject;
			if (this.objEditors.Count == 1) {
				nameQuery = firstNameable?.GetNameAsync ();
			}

			IObjectEventEditor events = editor as IObjectEventEditor;
			var newEventSet = new HashSet<IEventInfo> (events?.Events ?? Enumerable.Empty<IEventInfo> ());

			bool knownProperties = (editor?.KnownProperties?.Count ?? 0) > 0;
			string newTypeName = editor?.TargetType.Name;
			var newPropertySet = new HashSet<IPropertyInfo> (editor?.Properties ?? Enumerable.Empty<IPropertyInfo>());
			for (int i = 1; i < this.objEditors.Count; i++) {
				editor = this.objEditors[i];
				if (editor == null)
					continue;

				newPropertySet.IntersectWith (editor.Properties);

				if (editor is IObjectEventEditor) {
					events = (IObjectEventEditor)editor;
					newEventSet.IntersectWith (events.Events);
				}

				if (firstNameable == null)
					firstNameable = editor as INameableObject;

				if (newTypeName != editor.TargetType.Name)
					newTypeName = String.Format (PropertyEditing.Properties.Resources.MultipleTypesSelected, this.objEditors.Count);

				if (!knownProperties)
					knownProperties = (editor.KnownProperties?.Count ?? 0) > 0;
			}

			TypeName = newTypeName;

			if (knownProperties && this.knownEditors == null)
				this.knownEditors = new BidirectionalDictionary<KnownProperty, EditorViewModel> ();

			UpdateProperties (newPropertySet, removedEditors, newEditors);

			EventsEnabled = events != null;
			UpdateEvents (newEventSet, removedEditors, newEditors);

			string name = (this.objEditors.Count > 1) ? String.Format (PropertyEditing.Properties.Resources.MultipleObjectsSelected, this.objEditors.Count) : PropertyEditing.Properties.Resources.NoName;
			if (this.objEditors.Count == 1) {
				string tname = nameQuery?.Result;
				if (tname != null)
					name = tname;
			}

			SetNameable (firstNameable);
			SetCurrentObjectName (name, this.objEditors.Count > 1);
		}

		private void UpdateEvents (HashSet<IEventInfo> newSet, IObjectEditor[] removedEditors = null, IObjectEditor[] newEditors = null)
		{
			if (this.objEditors.Count > 1) {
				this.events.Clear ();
				return;
			}

			var toRemove = new List<EventViewModel> ();
			foreach (EventViewModel vm in this.events.ToArray ()) {
				if (!newSet.Remove (vm.Event)) {
					toRemove.Add (vm);
					vm.Editors.Clear ();
					continue;
				}

				if (removedEditors != null) {
					for (int i = 0; i < removedEditors.Length; i++)
						vm.Editors.Remove (removedEditors[i]);
				}

				if (newEditors != null) {
					for (int i = 0; i < newEditors.Length; i++)
						vm.Editors.Add (newEditors[i]);
				}
			}

			if (toRemove.Count > 0)
				this.events.RemoveRange (toRemove);
			if (newSet.Count > 0) {
				this.events.Reset (this.events.Concat (newSet.Select (i => new EventViewModel (TargetPlatform, i, this.objEditors))).OrderBy (e => e.Event.Name).ToArray());
			}
		}

		private void UpdateProperties (HashSet<IPropertyInfo> newSet, IObjectEditor[] removedEditors = null, IObjectEditor[] newEditors = null)
		{
			List<PropertyViewModel> toRemove = new List<PropertyViewModel> ();
			foreach (PropertyViewModel vm in this.editors.ToArray ()) {
				if (!newSet.Remove (vm.Property)) {
					toRemove.Add (vm);
					vm.Editors.Clear ();
					continue;
				}

				if (removedEditors != null) {
					for (int i = 0; i < removedEditors.Length; i++)
						vm.Editors.Remove (removedEditors[i]);
				}

				if (newEditors != null) {
					for (int i = 0; i < newEditors.Length; i++)
						vm.Editors.Add (newEditors[i]);
				}
			}

			if (toRemove.Count > 0)
				RemoveProperties (toRemove);
			if (newSet.Count > 0)
				AddProperties (newSet.Select (GetViewModel));
		}

		private async Task<IObjectEditor[]> AddEditorsAsync (IList newItems)
		{
			Task<IObjectEditor>[] newEditorTasks = new Task<IObjectEditor>[newItems.Count];
			for (int i = 0; i < newEditorTasks.Length; i++) {
				newEditorTasks[i] = TargetPlatform.EditorProvider.GetObjectEditorAsync (newItems[i]);
			}

			IObjectEditor[] newEditors = await Task.WhenAll (newEditorTasks);
			for (int i = 0; i < newEditors.Length; i++) {
				IObjectEditor editor = newEditors[i];
				if (editor == null)
					continue;

				var notifier = editor.Properties as INotifyCollectionChanged;
				if (notifier != null)
					notifier.CollectionChanged += OnObjectEditorPropertiesChanged;
			}

			this.objEditors.AddRange (newEditors);
			return newEditors;
		}

		private void OnObjectEditorPropertiesChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			UpdateMembers();
		}

		private PropertyViewModel GetViewModel (IPropertyInfo property)
		{
			return GetViewModelCore (property);
		}

		private PropertyViewModel GetViewModelCore (IPropertyInfo property)
		{
			Type[] interfaces = property.GetType ().GetInterfaces ();

			Type hasPredefinedValues = interfaces.FirstOrDefault (t => t.IsGenericType && t.GetGenericTypeDefinition () == typeof(IHavePredefinedValues<>));
			if (hasPredefinedValues != null) {
				bool combinable = (bool) hasPredefinedValues.GetProperty (nameof(IHavePredefinedValues<bool>.IsValueCombinable)).GetValue (property);
				Type type = combinable
					? typeof(CombinablePropertyViewModel<>).MakeGenericType (hasPredefinedValues.GenericTypeArguments[0])
					: typeof(PredefinedValuesViewModel<>).MakeGenericType (hasPredefinedValues.GenericTypeArguments[0]);

				return (PropertyViewModel) Activator.CreateInstance (type, TargetPlatform, property, this.objEditors);
			}

			if (ViewModelMap.TryGetValue (property.Type, out var vmFactory))
				return vmFactory (TargetPlatform, property, this.objEditors);
			
			return new StringPropertyViewModel (TargetPlatform, property, this.objEditors);
		}

		private Task busyTask;

		private static readonly Dictionary<Type, Func<TargetPlatform, IPropertyInfo, IEnumerable<IObjectEditor>, PropertyViewModel>> ViewModelMap = new Dictionary<Type, Func<TargetPlatform, IPropertyInfo, IEnumerable<IObjectEditor>, PropertyViewModel>> {
			{ typeof(string), (tp,p,e) => new StringPropertyViewModel (tp, p, e) },
			{ typeof(bool), (tp,p,e) => new PropertyViewModel<bool?> (tp, p, e) },
			{ typeof(float), (tp,p,e) => new NumericPropertyViewModel<float?> (tp, p, e) },
			{ typeof(double), (tp,p,e) => new NumericPropertyViewModel<double?> (tp, p, e) },
			{ typeof(int), (tp,p,e) => new NumericPropertyViewModel<int?> (tp, p, e) },
			{ typeof(long), (tp,p,e) => new NumericPropertyViewModel<long?> (tp, p, e) },
			{ typeof(CommonSolidBrush), (tp,p,e) => new BrushPropertyViewModel(tp, p, e, new[] {CommonBrushType.NoBrush, CommonBrushType.Solid, CommonBrushType.MaterialDesign, CommonBrushType.Resource }) },
			{ typeof(CommonColor), (tp,p,e) => new BrushPropertyViewModel(tp, p, e, new[] {CommonBrushType.NoBrush, CommonBrushType.Solid, CommonBrushType.MaterialDesign, CommonBrushType.Resource }) },
			{ typeof(CommonBrush), (tp,p,e) => new BrushPropertyViewModel(tp, p, e) },
			{ typeof(CommonPoint), (tp,p,e) => new PointPropertyViewModel (tp, p, e) },
			{ typeof(CommonSize), (tp,p,e) => new SizePropertyViewModel (tp, p, e) },
			{ typeof(CommonRectangle), (tp,p,e) => new RectanglePropertyViewModel (tp, p, e) },
			{ typeof(CommonThickness), (tp,p,e) => new ThicknessPropertyViewModel (tp, p, e) },
			{ typeof(IList), (tp,p,e) => new CollectionPropertyViewModel (tp,p,e) },
			{ typeof(BindingSource), (tp,p,e) => new PropertyViewModel<BindingSource> (tp, p, e) },
			{ typeof(Resource), (tp,p,e) => new PropertyViewModel<Resource> (tp, p, e) },
			{ typeof(object), (tp,p,e) => new ObjectPropertyViewModel (tp, p, e) },
			{ typeof(CommonRatio), (tp, p, e) => new RatioViewModel (tp, p, e) },
		};
	}
}
