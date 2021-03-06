﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using BrokenEvent.NanoXml;

namespace ObfuscarMappingParser
{
  internal class Mapping: IEntitySearcher
  {
    private readonly string filename;
    private DateTime lastModified;
    private List<RenamedClass> classes = new List<RenamedClass>();

    private long timingXML;
    private long timingParsing;
    private long timingSubclasses;
    private long timingUpdateNewNames;

    private int methodsCount;
    private int classesCount;
    private int subclassesCount;
    private int skippedCount;

    private List<string> modules = new List<string>();
    private List<string> namespaces = new List<string>();
    private List<string> namespacesObfuscated = new List<string>();

    private static string[] emptyArray = new string[0];

    private bool haveSystemEntities;

    public Mapping(string filename)
    {
      if (!File.Exists(filename))
        throw new ObfuscarParserException("File not exists", filename);

      this.filename = filename;
      LoadFile();
    }

    private void LoadFile()
    {      
      Stopwatch sw = new Stopwatch();
      sw.Start();
      NanoXmlDocument xml = NanoXmlDocument.LoadFromFile(filename);

      timingXML = sw.ElapsedMilliseconds;
      Debug.WriteLine("XML Loading: " + timingXML + " ms");
      sw.Reset();
      sw.Start();

      NanoXmlElement doc = xml.DocumentElement;
      NanoXmlElement types = (NanoXmlElement)doc["renamedTypes"];

      modules.Clear();
      namespaces.Clear();
      namespacesObfuscated.Clear();
      classes.Clear();
      haveSystemEntities = false;
      methodsCount = classesCount = subclassesCount = skippedCount = 0;
      lastModified = File.GetLastWriteTime(filename);

      List<RenamedClass> subclasses = new List<RenamedClass>();

      if (types != null)
        foreach (NanoXmlElement element in types.ChildElements)
        {
          if (string.Compare(element.Name, "renamedClass", StringComparison.Ordinal) == 0)
          {
            RenamedClass c = new RenamedClass(element, this);
            classesCount++;
            if (c.OwnerClassName == null)
            {
              classes.Add(c);
              if (c.Name.NameOld != null && c.Name.NameOld.Namespace != null)
                haveSystemEntities |= c.Name.NameOld.Namespace.StartsWith("System.");
            }
            else
              subclasses.Add(c);

            methodsCount += c.MethodsCount;
            if (c.ModuleNew != null && !modules.Contains(c.ModuleNew))
              modules.Add(c.ModuleNew);
            if (c.Name.NameOld != null && !string.IsNullOrEmpty(c.Name.NameOld.Namespace) && !namespaces.Contains(c.Name.NameOld.Namespace))
              namespaces.Add(c.Name.NameOld.Namespace);
            if (c.Name.NameNew != null && !string.IsNullOrEmpty(c.Name.NameNew.Namespace) && !namespacesObfuscated.Contains(c.Name.NameNew.Namespace))
              namespacesObfuscated.Add(c.Name.NameNew.Namespace);
          }
        }

      types = (NanoXmlElement)doc["skippedTypes"];
      if (types != null)
        foreach (NanoXmlElement element in types.ChildElements)
          if (string.Compare(element.Name, "skippedClass", StringComparison.Ordinal) == 0)
          {
            skippedCount++;
            classesCount++;
            RenamedClass c = new RenamedClass(element, this);
            if (c.OwnerClassName == null)
              classes.Add(c);
            else
              subclasses.Add(c);
          }


      timingParsing = sw.ElapsedMilliseconds;
      Debug.WriteLine("Parsing: " + timingParsing + " ms");
      sw.Reset();
      sw.Start();

      foreach (RenamedClass subclass in subclasses)
      {
        RenamedClass c = (RenamedClass)SearchForOldName(subclass.OwnerClassName);
        if (c == null)
          c = (RenamedClass)SearchForNewName(subclass.OwnerClassName);

        if (c != null)
        {
          c.Items.Add(subclass);
          subclass.OwnerClass = c;
          subclassesCount++;
          continue;
        }

        Debug.WriteLine("Failed to find root class: " + subclass.OwnerClassName);
        classes.Add(subclass);
      }

      timingSubclasses = sw.ElapsedMilliseconds;
      Debug.WriteLine("Subclasses processing: " + timingSubclasses + " ms");
      sw.Reset();
      sw.Start();

      foreach (RenamedClass c in classes)
        c.UpdateNewNames(this);

      timingUpdateNewNames = sw.ElapsedMilliseconds;
      Debug.WriteLine("Values updating: " + timingUpdateNewNames + " ms");
      Debug.WriteLine("Total elapsed: " + TimingTotal + " ms");
      sw.Stop();
    }

    public void Reload()
    {
      LoadFile();
    }

    public bool CheckModifications()
    {
      DateTime m = lastModified;
      lastModified = File.GetLastWriteTime(filename);
      return File.GetLastWriteTime(filename) > m;
    }

    public string Filename
    {
      get { return filename; }
    }

    public List<RenamedClass> Classes
    {
      get { return classes; }
    }

    public List<string> Modules
    {
      get { return modules; }
    }

    public List<string> Namespaces
    {
      get { return namespaces; }
    }

    public List<string> NamespacesObfuscated
    {
      get { return namespacesObfuscated; }
    }

    public void PurgeTreeNodes()
    {
      foreach (RenamedClass renamedClass in classes)
        renamedClass.PurgeTreeNodes();
    }

    #region Autocomplete

    public AutoCompleteStringCollection GetNewNamesCollection()
    {
      AutoCompleteStringCollection result = new AutoCompleteStringCollection();
      foreach (RenamedClass renamedClass in classes)
        foreach (RenamedBase item in renamedClass.GetChildItems())
        {
          if (item.Name.NameNew != null)
            result.Add(item.NameNewPlain);
        }

      return result;
    }

    public AutoCompleteStringCollection GetOldNamesCollection()
    {
      AutoCompleteStringCollection result = new AutoCompleteStringCollection();
      foreach (RenamedClass renamedClass in classes)
        foreach (RenamedBase item in renamedClass.GetChildItems())
          if (item.Name.NameOld != null)
          result.Add(item.NameOldPlain);

      return result;
    }

    #endregion

    #region Search

    public SearchResults Search(string s, bool allowSubstitution, bool filterPrefix)
    {
      if (string.IsNullOrEmpty(s))
        return null;

      s = s.TrimStart(' ');

      if (filterPrefix)
      {
        int i = s.IndexOf(' ');
        if (i != -1)
          s = s.Substring(i).TrimStart(' ');
      }

      Entity entity = s;
      if (entity.EntityType != EntityType.Method && entity.EntityType != EntityType.Constructor)
        return new SearchResults(s, entity, SearchItem(entity.Name));

      return new SearchResults(s, entity, SearchMethod(entity, allowSubstitution));
    }

    public SearchResults SearchOriginal(string s)
    {
      if (string.IsNullOrEmpty(s))
        return null;

      Entity entity = s;
      if (entity.EntityType != EntityType.Method && entity.EntityType != EntityType.Constructor)
        return new SearchResults(s, entity, SearchItemOriginal(entity.Name));

      return new SearchResults(s, entity, SearchMethodOriginal(entity));
    }

    private IEnumerable<INamedEntity> SearchMethod(Entity entity, bool allowSubstitution)
    {
      bool hasAdded = false;
      foreach (RenamedBase item in SearchItem(entity.Name))
      {
        if (item.EntityType != EntityType.Method)
          continue;

        RenamedItem i = (RenamedItem)item;
        if (i.CompareParams(entity.MethodParams))
        {
          hasAdded = true;
          yield return i;
        }
      }

      if(hasAdded || !allowSubstitution)
        yield break;

      string[] values = entity.Name.Namespace != null ? entity.Name.Namespace.Split('.') : emptyArray;

      List<EntityName> methodParams = new List<EntityName>(entity.MethodParams);
      SubstituteParams(methodParams);

      foreach (RenamedClass renamedClass in classes)
      {
        foreach (RenamedBase item in renamedClass.Search(values, 0))
          yield return new Entity(entity, item, methodParams);
      }
    }

    private IEnumerable<INamedEntity> SearchMethodOriginal(Entity entity)
    {
      foreach (RenamedBase item in SearchItemOriginal(entity.Name))
      {
        if (item.EntityType != EntityType.Method)
          continue;

        RenamedItem i = (RenamedItem)item;
        if (i.CompareParams(entity.MethodParams))
        {
          yield return i;
        }
      }
    }

    private void SubstituteParams(IList<EntityName> p)
    {
      for (int i = 0; i < p.Count; i++)
      {
        EntityName paramName = null;

        try
        {
          string[] values = p[i].PathName.Split('.');
          foreach (RenamedBase item in SearchItem(values))
            paramName = item.Name.NameOld;
        }
        catch { }

        if (paramName == null)
          foreach (RenamedClass item in SearchClassNoNs(p[i].Name))
            paramName = item.Name.NameOld;

        if (paramName != null)
          p[i] = paramName;
      }
    }

    private IEnumerable<RenamedBase> SearchItem(string[] values)
    {
      foreach (RenamedClass renamedClass in classes)
      {
        foreach (RenamedBase item in renamedClass.Search(values, 0))
          yield return item;
      }
    }

    private IEnumerable<RenamedBase> SearchItem(EntityName name)
    {
      string[] values;
      try
      {
        values = name.PathName.Split('.');
      }
      catch
      {
        yield break;
      }

      foreach (RenamedBase renamedItem in SearchItem(values))
        yield return renamedItem;
    }

    private IEnumerable<RenamedBase> SearchItemOriginal(string[] values)
    {
      foreach (RenamedClass renamedClass in classes)
      {
        foreach (RenamedBase item in renamedClass.SearchOriginal(values, 0))
          yield return item;
      }
    }

    private IEnumerable<RenamedBase> SearchItemOriginal(EntityName name)
    {
      string[] values;
      try
      {
        values = name.PathName.Split('.');
      }
      catch
      {
        yield break;
      }

      foreach (RenamedBase renamedItem in SearchItemOriginal(values))
        yield return renamedItem;
    }

    private IEnumerable<RenamedClass> SearchClassNoNs(string name)
    {
      foreach (RenamedClass c in classes)
      {
        if (string.Compare(c.Name.NameNew.Name, name, StringComparison.Ordinal) == 0)
          yield return c;
      }
    }

    #endregion

    #region Crashlog processing

    public string ProcessCrashlogText(string text, bool filterPrefix = true)
    {
      List<SearchResults> results = ProcessCrashlog(text, filterPrefix);
      StringBuilder sb = new StringBuilder();
      foreach (SearchResults result in results)
        sb.AppendLine(result.ToString());

      return sb.ToString();
    }

    public List<SearchResults> ProcessCrashlog(string text, bool filterPrefix = true)
    {
      string[] strings = text.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
      List<SearchResults> results = new List<SearchResults>();

      foreach (string s in strings)
        results.Add(ProcessCrashlogLine(s, filterPrefix));

      return results;
    }

    private SearchResults ProcessCrashlogLine(string text, bool filterPrefix)
    {
      return Search(text, true, filterPrefix);
    }

    #endregion

    #region IEntitySearcher

    public RenamedBase SearchForNewName(EntityName target)
    {
      foreach (RenamedClass renamedClass in classes)
      {
        RenamedClass c = renamedClass.SearchForNewName(target);
        if (c != null)
          return c;
      }

      return null;
    }

    public RenamedBase SearchForOldName(EntityName target)
    {
      foreach (RenamedClass renamedClass in classes)
      {
        RenamedClass c = renamedClass.SearchForOldName(target);
        if (c != null)
          return c;
      }

      return null;
    }

    public bool HaveSystemEntities
    {
      get { return haveSystemEntities; }
    }

    #endregion

    #region Timings

    public long TimingXml
    {
      get { return timingXML; }
    }

    public long TimingParsing
    {
      get { return timingParsing; }
    }

    public long TimingSubclasses
    {
      get { return timingSubclasses; }
    }

    public long TimingUpdateNewNames
    {
      get { return timingUpdateNewNames; }
    }

    public long TimingTotal
    {
      get { return timingXML + timingParsing + timingSubclasses + timingUpdateNewNames; }
    }

    #endregion

    #region Statistics

    public int TotalMethodsCount
    {
      get { return methodsCount; }
    }

    public int TotalClassesCount
    {
      get { return classesCount; }
    }

    public int TotalSubclassesCount
    {
      get { return subclassesCount; }
    }

    public int NamespacesCount
    {
      get { return namespaces.Count; }
    }

    public int ModulesCount
    {
      get { return modules.Count; }
    }

    public int SkippedEntities
    {
      get { return skippedCount; }
    }

    public int ObfuscatedNamespacesCount
    {
      get { return namespacesObfuscated.Count; }
    }

    #endregion
  }
}
