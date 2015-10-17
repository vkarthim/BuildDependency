﻿// Copyright (c) 2014-2015 Eberhard Beilharz
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using Eto.Forms;
using BuildDependency.TeamCity;
using BuildDependency.TeamCity.RestClasses;
using BuildDependency.Artifacts;
using System.IO;
using Eto.Drawing;

namespace BuildDependency.Dialogs
{
	public class BuildDependencyManagerDialog: Form
	{
		private List<ArtifactTemplate> _artifacts;
		private List<Server> _servers;
		private readonly GridView _gridView;

		public BuildDependencyManagerDialog()
		{
			_artifacts = new List<ArtifactTemplate>();
			Title = "Build Dependency Manager";
			ClientSize = new Size(700, 400);

			Menu = new MenuBar
			{
				Items =
				{
					new ButtonMenuItem
					{
						Text = "&File",
						Items =
						{
							new Command(OnFileNew) { MenuText = "&New" },
							new Command(OnFileOpen) { MenuText = "&Open" },
							new Command(OnFileSave) { MenuText = "&Save" },
							new Command(OnFileImport) { MenuText = "&Import" },
						}
					},
					new ButtonMenuItem
					{
						Text = "&Tools",
						Items =
						{
							new Command(OnToolsServers) { MenuText = "&Servers" },
							//new Command() { MenuText = "S&ort" }
						}
					}
				},
				QuitItem = new Command((sender, e) => Application.Instance.Quit())
				{
					MenuText = "E&xit", 
				}
			};

			_gridView = new GridView();
			_gridView.GridLines = GridLines.Both;
			_gridView.ShowHeader = true;
			_gridView.Size = new Size(680, 350);
			_gridView.DataStore = new SelectableFilterCollection<ArtifactTemplate>(_gridView, _artifacts);
			_gridView.Columns.Add(new GridColumn
				{
					HeaderText = "Artifacts source",
					DataCell = new TextBoxCell("Source"),
					AutoSize = true,
					Resizable = true,
					Sortable = true,
				});
			_gridView.Columns.Add(new GridColumn
				{
					HeaderText = "Artifacts path",
					DataCell = new TextBoxCell("PathRules"),
					AutoSize = true,
					Resizable = true,
					Sortable = true
				});

			var button = new Button();
			button.Text = "Add Artifact";
			button.Click += OnAddArtifact;

			var stacklayout = new StackLayout() { Padding = new Padding(10), Spacing = 5 };

			stacklayout.Items.Add(new StackLayoutItem(_gridView, HorizontalAlignment.Right, true));
			stacklayout.Items.Add(new StackLayoutItem(button, HorizontalAlignment.Right));
			Content = stacklayout;

			OnFileNew(this, EventArgs.Empty);
		}

		private void OnAddArtifact(object sender, EventArgs e)
		{
			using (var dlg = new AddOrEditArtifactDependencyDialog(true, _servers))
			{
				dlg.ShowModal(this);

				if (dlg.Result)
				{
					var artifact = dlg.GetArtifact();
					_artifacts.Add(artifact);
				}
			}
		}

		private void OnEditArtifact(object sender, EventArgs e)
		{
			var item = _gridView.SelectedItem as ArtifactTemplate;
			if (item == null)
				return;
			var artifactIndex = _artifacts.IndexOf(item);
			using (var dlg = new AddOrEditArtifactDependencyDialog(_servers, item))
			{
				dlg.ShowModal();
				if (dlg.Result)
				{
					var artifact = dlg.GetArtifact();
					_artifacts[artifactIndex] = artifact;
				}
			}
		}

		private void OnDeleteArtifact(object sender, EventArgs e)
		{
			var item = _gridView.SelectedItem as ArtifactTemplate;
			if (item == null)
				return;
			_artifacts.Remove(item);
		}

		private void OnFileNew(object sender, EventArgs e)
		{
			_artifacts.Clear();
			_servers = new List<Server>();
			var server = Server.CreateServer(ServerType.TeamCity);
			server.Name = "TC";
			server.Url = "http://build.palaso.org";
			_servers.Add(server);
		}

		private void OnFileOpen(object sender, EventArgs e)
		{
			string fileName = null;
			using (var dlg = new OpenFileDialog())
			{
				dlg.Filters.Add(new FileDialogFilter("Dependency File (*.dep)", "*.dep"));
				dlg.Filters.Add(new FileDialogFilter("All Files (*.*)", "*"));
				dlg.CurrentFilterIndex = 0;
				if (dlg.ShowDialog(this) == DialogResult.Ok)
				{
					fileName = dlg.FileName;
				}
			}
			_artifacts = DependencyFile.LoadFile(fileName);
		}

		private void OnFileSave(object sender, EventArgs e)
		{
			using (var dlg = new SaveFileDialog())
			{
				dlg.Filters.Add(new FileDialogFilter("Dependency File (*.dep)", "*.dep"));
				dlg.Filters.Add(new FileDialogFilter("All Files (*.*)", "*"));
				dlg.CurrentFilterIndex = 0;

				if (dlg.ShowDialog(this) == DialogResult.Ok)
				{
					var fileName = dlg.FileName;
					if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
						fileName += ".dep";
					DependencyFile.SaveFile(fileName, _servers, _artifacts);
					JobsFile.WriteJobsFile(Path.ChangeExtension(fileName, ".files"), _artifacts);
				}
			}
		}

		private void OnFileImport(object sender, EventArgs e)
		{
			using (var dlg = new ImportDialog(_servers))
			{
				dlg.ShowModal();
				if (dlg.Result)
				{
					var configId = dlg.SelectedBuildConfig;
					var condition = dlg.Condition;

					var server = dlg.Server as TeamCityApi;
					if (server == null)
						return;
					foreach (var dep in server.GetArtifactDependencies(configId))
					{
						var artifact = new ArtifactTemplate(server, new ArtifactProperties(dep.Properties));
						artifact.Condition = condition;
						_artifacts.Add(artifact);
					}
				}
			}
		}

		private void OnToolsServers(object sender, EventArgs e)
		{
			using (var dlg = new ServersDialog(_servers))
			{
				dlg.ShowModal();
				if (dlg.Result)
				{
					_servers = dlg.Servers;
				}
			}
		}

		//		private void OnToolsSort(object sender, EventArgs e)
		//		{
		//			_artifacts.Sort((x, y) => x.ConfigName.CompareTo(y.ConfigName));
		//			_store.Clear();
		//			foreach (var artifact in _artifacts)
		//			{
		//				int row = _store.AddRow();
		//				AddArtifactToStore(row, artifact);
		//			}
		//		}
		//
		//		private void HandleButtonPressed(object sender, ButtonEventArgs e)
		//		{
		//			if (e.Button != PointerButton.Right)
		//				return;
		//
		//			var row = _listView.GetRowAtPosition(e.Position);
		//			var menu = new Menu();
		//			var menuItem = new MenuItem("_Edit");
		//			menuItem.Tag = row;
		//			menuItem.Clicked += OnEditArtifact;
		//			menu.Items.Add(menuItem);
		//			menuItem = new MenuItem("_Delete");
		//			menuItem.Tag = row;
		//			menuItem.Clicked += OnDeleteArtifact;
		//			menu.Items.Add(menuItem);
		//			menu.Popup();
		//		}
		//

	}

}

