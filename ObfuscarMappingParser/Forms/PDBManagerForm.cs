﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using BrokenEvent.Shared;
using BrokenEvent.Shared.Algorithms;
using BrokenEvent.TaskDialogs;
using BrokenEvent.TaskDialogs.Dialogs;

namespace ObfuscarMappingParser
{
  partial class PDBManagerForm : BaseForm
  {
    private readonly IList<PdbFile> files;
    private readonly MainForm mainForm;

    public PDBManagerForm(IList<PdbFile> files, MainForm mainForm)
    {
      this.files = files;
      this.mainForm = mainForm;
      InitializeComponent();
      chFilename.Width = lvList.ClientSize.Width;
      lvList.SmallImageList = mainForm.IconsList;

      LoadList();
    }

    private void LoadList()
    {
      lvList.Items.Clear();
      lvList.BeginUpdate();

      foreach (PdbFile pdbFile in files)
      {
        ListViewItem item = new ListViewItem(PathUtils.GetFilename(pdbFile.Filename));
        item.SubItems.Add(pdbFile.Resolver.PdbGuid.ToString());
        item.Tag = pdbFile;
        item.ImageIndex = mainForm.ICON_PDB;
        item.ToolTipText =
          $"Filename: {pdbFile.Filename}\nServer data: {(string.IsNullOrEmpty(pdbFile.Resolver.SourceServerData) ? "none" : pdbFile.Resolver.SourceServerData)}";
        lvList.Items.Add(item);
      }

      lvList.EndUpdate();
    }

    private void lvList_Resize(object sender, EventArgs e)
    {
      chFilename.Width = lvList.ClientSize.Width - chGuid.Width;
    }

    private void lvList_SelectedIndexChanged(object sender, EventArgs e)
    {
      btnDetach.Enabled = lvList.SelectedItems.Count > 0;
    }

    private void btnDetach_Click(object sender, EventArgs e)
    {
      if (TaskDialogHelper.ShowTaskDialog(
            Handle,
            "Detach PDBs",
            "Detach selected PDB files?",
            null,
            TaskDialogStandardIcon.Information, 
            new string[]{"Detach", "Don't detach"},
            null,
            new TaskDialogResult[]{TaskDialogResult.Yes, TaskDialogResult.No}
          ) != TaskDialogResult.Yes)
        return;

      foreach (ListViewItem item in lvList.SelectedItems)
      {
        PdbFile file = (PdbFile)item.Tag;
        files.Remove(file);
        Configs.Instance.RemoveRecentPdb(mainForm.Mapping.Filename, file.Filename);
        item.Remove();
      }
    }

    private void PDBManagerForm_DragEnter(object sender, DragEventArgs e)
    {
      e.Effect = DragDropEffects.None;
      if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        return;

      string[] pdbs = (string[])e.Data.GetData(DataFormats.FileDrop);
      bool havePdb = false;
      foreach (string pdb in pdbs)
      {
        if (string.Compare(Path.GetExtension(pdb), ".pdb", StringComparison.OrdinalIgnoreCase) == 0 && File.Exists(pdb))
        {
          havePdb = true;
          break;
        }
      }

      if (!havePdb)
        return;

      e.Effect = DragDropEffects.Move;
    }

    private void btnAttach_Click(object sender, EventArgs e)
    {
      if (mainForm.CallAttachPdb(this))
        LoadList();
    }

    private void PDBManagerForm_DragDrop(object sender, DragEventArgs e)
    {
      if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        return;

      string[] pdbs = (string[])e.Data.GetData(DataFormats.FileDrop);
      bool havePdb = false;
      foreach (string pdb in pdbs)
      {
        if (string.Compare(Path.GetExtension(pdb), ".pdb", StringComparison.OrdinalIgnoreCase) == 0 && File.Exists(pdb))
        {
          if (mainForm.AttachPDB(pdb, this))
          {
            Configs.Instance.AddRecentPdb(mainForm.Mapping.Filename, pdb);
            havePdb = true;
          }
        }
      }

      if (havePdb)
        LoadList();
    }
  }
}
