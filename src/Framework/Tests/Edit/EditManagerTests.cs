using System;
using System.Linq;
using System.Collections.Generic;
using N2.Configuration;
using N2.Tests.Fakes;
using NUnit.Framework;
using N2.Edit;
using N2.Definitions;
using System.Web.UI;
using System.Web.UI.WebControls;
using N2.Web.UI.WebControls;
using Rhino.Mocks;
using N2.Persistence;
using N2.Tests.Edit.Items;
using System.Security.Principal;
using N2.Persistence.Proxying;
using N2.Definitions.Static;
using N2.Security;
using N2.Web;

namespace N2.Tests.Edit
{
	public abstract class EditManagerTests : TypeFindingBase
	{
		protected DefinitionManager definitions;
		protected EditManager editManager;
		protected IVersionManager versioner;

		protected override Type[] GetTypes()
		{
			return new[]
			       	{
			       		typeof (ComplexContainersItem),
			       		typeof (ItemWithRequiredProperty),
			       		typeof (ItemWithModification),
			       		typeof (NotVersionableItem),
			       		typeof (LegacyNotVersionableItem),
			       		typeof (ItemWithSecuredContainer)
			       	};
		}

		[SetUp]
		public override void SetUp()
		{
			base.SetUp();
			DefinitionBuilder builder = new DefinitionBuilder(typeFinder, new EngineSection());
			IItemNotifier notifier = mocks.DynamicMock<IItemNotifier>();
			mocks.Replay(notifier);
			var changer = new N2.Edit.Workflow.StateChanger();
			definitions = new DefinitionManager(new [] {new DefinitionProvider(builder)}, new ContentActivator(changer, notifier, new EmptyProxyFactory()));

			versioner = mocks.StrictMock<IVersionManager>();
			var urls = new FakeEditUrlManager();
			editManager = new EditManager(definitions, persister, versioner, null, null, null, urls, changer, new EditableHierarchyBuilder(new SecurityManager(new ThreadContext(), new EditSection()), new EngineSection()), new EditSection());
			editManager.EnableVersioning = true;

			var engine = new FakeEngine();
			engine.AddComponentInstance("editManager", typeof(IEditManager), editManager);

			engine.AddComponentInstance("editSection", typeof(EditSection), new EditSection());

			Context.Replace(engine);
		}

		protected ContainableContext[] AddEditors(ComplexContainersItem item)
		{
			Type itemType = item.GetContentType();
			Control editorContainer = new Control();
			return editManager.AddEditors(definitions.GetDefinition(itemType), item, editorContainer, CreatePrincipal("someone")).ToArray();
		}

		protected bool savingVersionEventInvoked = false;
		protected void editManager_SavingVersion(object sender, CancellableItemEventArgs e)
		{
			savingVersionEventInvoked = true;
		}

		[Obsolete]
		protected void DoTheSaving(IPrincipal user, IItemEditor editor)
		{
			using (mocks.Playback())
			{
				editManager.Save(editor.CurrentItem, editor.Editors, editor.VersioningMode, user);
			}
		}

		[Obsolete]
		protected IItemEditor SimulateEditor(ContentItem item, ItemEditorVersioningMode versioningMode)
		{
			IItemEditor editor = mocks.StrictMock<IItemEditor>();

			ContainableContext[] editors = CreateEditorsForComplexContainersItem(item);

			using (mocks.Record())
			{
				Expect.On(editor).Call(editor.CurrentItem).Return(item).Repeat.Any();
				Expect.On(editor).Call(editor.Editors).Return(editors);
				Expect.On(editor).Call(editor.VersioningMode).Return(versioningMode).Repeat.Any();
			}
			return editor;
		}

		protected static ContainableContext[] CreateEditorsForComplexContainersItem(ContentItem item)
		{
			Dictionary<string, Control> editors = new Dictionary<string, Control>();

			editors["MyProperty0"] = new TextBox();
			editors["MyProperty1"] = new TextBox();
			editors["MyProperty2"] = new TextBox();
			editors["MyProperty3"] = new FreeTextArea();
			editors["MyProperty4"] = new CheckBox();
			
			((TextBox)editors["MyProperty0"]).Text = "one";
			((TextBox)editors["MyProperty1"]).Text = "two";
			((TextBox)editors["MyProperty2"]).Text = "three";
			((FreeTextArea)editors["MyProperty3"]).Text = "rock";
			((CheckBox)editors["MyProperty4"]).Checked = true;

			return editors.Select(e => ContainableContext.WithControl(e.Key, item, e.Value)).ToArray();
		}

		protected static void AssertItemHasValuesFromEditors(ComplexContainersItem item)
		{
			Assert.AreEqual("one", item.MyProperty0);
			Assert.AreEqual("two", item.MyProperty1);
			Assert.AreEqual("three", item.MyProperty2);
			Assert.AreEqual("rock", item.MyProperty3);
			Assert.IsTrue(item.MyProperty4);
		}
	}
}
