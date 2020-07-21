using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Server.Commands.Generic;
using Server.Items;
using Server.Network;

namespace Server.Commands
{
  public class Docs
  {
    private static Dictionary<Type, TypeInfo> m_Types;
    private static Dictionary<string, List<TypeInfo>> m_Namespaces;

    public static void Initialize()
    {
      CommandSystem.Register("DocGen", AccessLevel.Administrator, DocGen_OnCommand);
    }

    [Usage("DocGen")]
    [Description("Generates RunUO documentation.")]
    private static void DocGen_OnCommand(CommandEventArgs e)
    {
      World.Broadcast(0x35, true, "Documentation is being generated, please wait.");
      Console.WriteLine("Documentation is being generated, please wait.");

      NetState.Pause();

      DateTime startTime = DateTime.UtcNow;

      bool generated = Document();

      DateTime endTime = DateTime.UtcNow;

      NetState.Resume();

      if (generated)
      {
        World.Broadcast(0x35, true, "Documentation has been completed. The entire process took {0:F1} seconds.",
          (endTime - startTime).TotalSeconds);
        Console.WriteLine("Documentation complete.");
      }
      else
      {
        World.Broadcast(0x35, true,
          "Docmentation failed: Documentation directories are locked and in use. Please close all open files and directories and try again.");
        Console.WriteLine("Documentation failed.");
      }
    }

    private static void LoadTypes(Assembly a, Assembly[] asms)
    {
      Type[] types = a.GetTypes();

      for (int i = 0; i < types.Length; ++i)
      {
        Type type = types[i];

        string nspace = type.Namespace;

        if (nspace == null || type.IsSpecialName)
          continue;

        TypeInfo info = new TypeInfo(type);
        m_Types[type] = info;

        if (!m_Namespaces.TryGetValue(nspace, out List<TypeInfo> nspaces))
          m_Namespaces[nspace] = nspaces = new List<TypeInfo>();

        nspaces.Add(info);

        Type baseType = info.m_BaseType;

        if (baseType != null && InAssemblies(baseType, asms))
        {
          m_Types.TryGetValue(baseType, out TypeInfo baseInfo);

          if (baseInfo == null)
            m_Types[baseType] = baseInfo = new TypeInfo(baseType);

          baseInfo.m_Derived ??= new List<TypeInfo>();

          baseInfo.m_Derived.Add(info);
        }

        Type decType = info.m_Declaring;

        if (decType != null)
        {
          m_Types.TryGetValue(decType, out TypeInfo decInfo);

          if (decInfo == null)
            m_Types[decType] = decInfo = new TypeInfo(decType);

          decInfo.m_Nested ??= new List<TypeInfo>();

          decInfo.m_Nested.Add(info);
        }

        for (int j = 0; j < info.m_Interfaces.Length; ++j)
        {
          Type iface = info.m_Interfaces[j];

          if (!InAssemblies(iface, asms))
            continue;

          m_Types.TryGetValue(iface, out TypeInfo ifaceInfo);

          if (ifaceInfo == null)
            m_Types[iface] = ifaceInfo = new TypeInfo(iface);

          ifaceInfo.m_Derived ??= new List<TypeInfo>();

          ifaceInfo.m_Derived.Add(info);
        }
      }
    }

    private static bool InAssemblies(Type t, Assembly[] asms)
    {
      Assembly a = t.Assembly;

      for (int i = 0; i < asms.Length; ++i)
        if (a == asms[i])
          return true;

      return false;
    }

    private static void DocumentLoadedTypes()
    {
      using StreamWriter indexHtml = GetWriter("docs/", "overview.html");
      indexHtml.WriteLine("<html>");
      indexHtml.WriteLine("   <head>");
      indexHtml.WriteLine("      <title>RunUO Documentation - Class Overview</title>");
      indexHtml.WriteLine("   </head>");
      indexHtml.WriteLine(
        "   <body bgcolor=\"white\" style=\"font-family: Courier New\" text=\"#000000\" link=\"#000000\" vlink=\"#000000\" alink=\"#808080\">");
      indexHtml.WriteLine("      <h4><a href=\"index.html\">Back to the index</a></h4>");
      indexHtml.WriteLine("      <h2>Namespaces</h2>");

      SortedList<string, List<TypeInfo>> nspaces = new SortedList<string, List<TypeInfo>>(m_Namespaces);

      foreach (KeyValuePair<string, List<TypeInfo>> kvp in nspaces)
      {
        kvp.Value.Sort(new TypeComparer());

        SaveNamespace(kvp.Key, kvp.Value, indexHtml);
      }

      indexHtml.WriteLine("   </body>");
      indexHtml.WriteLine("</html>");
    }

    private static void SaveNamespace(string name, List<TypeInfo> types, StreamWriter indexHtml)
    {
      string fileName = GetFileName("docs/namespaces/", name, ".html");

      indexHtml.WriteLine("      <a href=\"namespaces/{0}\">{1}</a><br>", fileName, name);

      using StreamWriter nsHtml = GetWriter("docs/namespaces/", fileName);
      nsHtml.WriteLine("<html>");
      nsHtml.WriteLine("   <head>");
      nsHtml.WriteLine("      <title>RunUO Documentation - Class Overview - {0}</title>", name);
      nsHtml.WriteLine("   </head>");
      nsHtml.WriteLine(
        "   <body bgcolor=\"white\" style=\"font-family: Courier New\" text=\"#000000\" link=\"#000000\" vlink=\"#000000\" alink=\"#808080\">");
      nsHtml.WriteLine("      <h4><a href=\"../overview.html\">Back to the namespace index</a></h4>");
      nsHtml.WriteLine("      <h2>{0}</h2>", name);

      for (int i = 0; i < types.Count; ++i)
        SaveType(types[i], nsHtml, fileName, name);

      nsHtml.WriteLine("   </body>");
      nsHtml.WriteLine("</html>");
    }

    private static void SaveType(TypeInfo info, StreamWriter nsHtml, string nsFileName, string nsName)
    {
      if (info.m_Declaring == null)
        nsHtml.WriteLine($"      <!-- DBG-ST -->{info.LinkName("../types/")}<br>");

      using StreamWriter typeHtml = GetWriter(info.FileName);
      typeHtml.WriteLine("<html>");
      typeHtml.WriteLine("   <head>");
      typeHtml.WriteLine("      <title>RunUO Documentation - Class Overview - {0}</title>", info.TypeName);
      typeHtml.WriteLine("   </head>");
      typeHtml.WriteLine(
        "   <body bgcolor=\"white\" style=\"font-family: Courier New\" text=\"#000000\" link=\"#000000\" vlink=\"#000000\" alink=\"#808080\">");
      typeHtml.WriteLine("      <h4><a href=\"../namespaces/{0}\">Back to {1}</a></h4>", nsFileName, nsName);

      if (info.m_Type.IsEnum)
        WriteEnum(info, typeHtml);
      else
        WriteType(info, typeHtml);

      typeHtml.WriteLine("   </body>");
      typeHtml.WriteLine("</html>");
    }

    public static void FormatGeneric(Type type, out string typeName, out string fileName, out string linkName)
    {
      string name = null;
      string fnam = null;
      string link = null;

      if (type.IsGenericType)
      {
        int index = type.Name.IndexOf('`');

        if (index > 0)
        {
          string rootType = type.Name.Substring(0, index);

          StringBuilder nameBuilder = new StringBuilder(rootType);
          StringBuilder fnamBuilder = new StringBuilder($"docs/types/{SanitizeType(rootType)}");
          StringBuilder linkBuilder;
          linkBuilder = DontLink(type) ?
            new StringBuilder($"<font color=\"blue\">{rootType}</font>") :
            new StringBuilder($"<a href=\"@directory@{rootType}-T-.html\">{rootType}</a>");

          nameBuilder.Append("&lt;");
          fnamBuilder.Append("-");
          linkBuilder.Append("&lt;");

          Type[] typeArguments = type.GetGenericArguments();

          for (int i = 0; i < typeArguments.Length; i++)
          {
            if (i != 0)
            {
              nameBuilder.Append(',');
              fnamBuilder.Append(',');
              linkBuilder.Append(',');
            }

            string sanitizedName = SanitizeType(typeArguments[i].Name);
            string aliasedName = AliasForName(sanitizedName);

            nameBuilder.Append(sanitizedName);
            fnamBuilder.Append("T");
            if (DontLink(typeArguments[i]))
              linkBuilder.Append($"<font color=\"blue\">{aliasedName}</font>");
            else
              linkBuilder.Append(
                $"<a href=\"@directory@{aliasedName}.html\">{aliasedName}</a>");
          }

          nameBuilder.Append("&gt;");
          fnamBuilder.Append("-");
          linkBuilder.Append("&gt;");

          name = nameBuilder.ToString();
          fnam = fnamBuilder.ToString();
          link = linkBuilder.ToString();
        }
      }

      typeName = name ?? type.Name;

      fileName = fnam == null ? $"docs/types/{SanitizeType(type.Name)}.html" : $"{fnam}.html";

      if (link == null)
        linkName = DontLink(type) ? $"<font color=\"blue\">{SanitizeType(type.Name)}</font>"
          : $"<a href=\"@directory@{SanitizeType(type.Name)}.html\">{SanitizeType(type.Name)}</a>";
      else
        linkName = link;

      // Console.WriteLine( typeName+":"+fileName+":"+linkName );
    }

    public static string SanitizeType(string name)
    {
      bool anonymousType = name.Contains("<");
      StringBuilder sb = new StringBuilder(name);
      for (int i = 0; i < ReplaceChars.Length; ++i)
        sb.Replace(ReplaceChars[i], '-');

      if (anonymousType) return $"(Anonymous-Type){sb}";
      return sb.ToString();
    }

    public static string AliasForName(string name)
    {
      for (int i = 0; i < m_AliasLength; ++i)
        if (m_Aliases[i, 0] == name)
          return m_Aliases[i, 1];
      return name;
    }

    /*
    // For stuff we don't want to links to
    private static string[] m_DontLink = new string[]
    {
        "List",
        "Stack",
        "Queue",
        "Dictionary",
        "LinkedList",
        "SortedList",
        "SortedDictionary",
        "IComparable",
        "IComparer",
        "ICloneable",
        "Type"
    };

    public static bool DontLink( string name )
    {
      foreach( string dontLink in m_DontLink )
        if (dontLink == name ) return true;
      return false;
    }
    */
    public static bool DontLink(Type type)
    {
      if (type.Name == "T" || string.IsNullOrEmpty(type.Namespace) || m_Namespaces == null)
        return true;

      if (type.Namespace.StartsWith("Server"))
        return false;

      return !m_Namespaces.ContainsKey(type.Namespace);
    }

    private class MemberComparer : IComparer
    {
      public int Compare(object x, object y)
      {
        if (x == y)
          return 0;

        ConstructorInfo aCtor = x as ConstructorInfo;
        ConstructorInfo bCtor = y as ConstructorInfo;

        PropertyInfo aProp = x as PropertyInfo;
        PropertyInfo bProp = y as PropertyInfo;

        MethodInfo aMethod = x as MethodInfo;
        MethodInfo bMethod = y as MethodInfo;

        bool aStatic = GetStaticFor(aCtor, aProp, aMethod);
        bool bStatic = GetStaticFor(bCtor, bProp, bMethod);

        if (aStatic && !bStatic)
          return -1;
        if (!aStatic && bStatic)
          return 1;

        int v = 0;

        if (aCtor != null)
        {
          if (bCtor == null)
            v = -1;
        }
        else if (bCtor != null)
        {
          v = 1;
        }
        else if (aProp != null)
        {
          if (bProp == null)
            v = -1;
        }
        else if (bProp != null)
        {
          v = 1;
        }

        if (v == 0)
          v = GetNameFrom(aCtor, aProp, aMethod).CompareTo(GetNameFrom(bCtor, bProp, bMethod));

        if (v == 0 && aCtor != null && bCtor != null)
          v = aCtor.GetParameters().Length.CompareTo(bCtor.GetParameters().Length);
        else if (v == 0 && aMethod != null && bMethod != null)
          v = aMethod.GetParameters().Length.CompareTo(bMethod.GetParameters().Length);

        return v;
      }

      private bool GetStaticFor(ConstructorInfo ctor, PropertyInfo prop, MethodInfo method)
      {
        if (ctor != null)
          return ctor.IsStatic;
        if (method != null)
          return method.IsStatic;

        if (prop != null)
        {
          MethodInfo getMethod = prop.GetGetMethod();
          MethodInfo setMethod = prop.GetGetMethod();

          return getMethod?.IsStatic == true || setMethod?.IsStatic == true;
        }

        return false;
      }

      private string GetNameFrom(ConstructorInfo ctor, PropertyInfo prop, MethodInfo method) => ctor?.DeclaringType?.Name ?? prop?.Name ?? method?.Name ?? "";
    }

    private class TypeComparer : IComparer<TypeInfo>
    {
      public int Compare(TypeInfo x, TypeInfo y) =>
        x == null && y == null ? 0 : x == null ? -1 : y == null ? 1 :
        x.TypeName.CompareTo(y.TypeName);
    }

    private class TypeInfo
    {
      public List<TypeInfo> m_Derived, m_Nested;
      private readonly string m_FileName;
      private readonly string m_TypeName;
      private readonly string m_LinkName;
      public readonly Type[] m_Interfaces;
      public readonly Type m_Type;
      public readonly Type m_BaseType;
      public readonly Type m_Declaring;

      public TypeInfo(Type type)
      {
        m_Type = type;

        m_BaseType = type.BaseType;
        m_Declaring = type.DeclaringType;
        m_Interfaces = type.GetInterfaces();

        FormatGeneric(m_Type, out m_TypeName, out m_FileName, out m_LinkName);
      }

      public string FileName => m_FileName;
      public string TypeName => m_TypeName;

      public string LinkName(string dirRoot) => m_LinkName.Replace("@directory@", dirRoot);
    }

    private static readonly char[] ReplaceChars = "<>".ToCharArray();

    public static string GetFileName(string root, string name, string ext)
    {
      if (name.IndexOfAny(ReplaceChars) >= 0)
      {
        StringBuilder sb = new StringBuilder(name);

        for (int i = 0; i < ReplaceChars.Length; ++i) sb.Replace(ReplaceChars[i], '-');

        name = sb.ToString();
      }

      int index = 0;
      string file = string.Concat(name, ext);

      while (File.Exists(Path.Combine(root, file))) file = string.Concat(name, ++index, ext);

      return file;
    }

    private static readonly string m_RootDirectory = Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);

    private static void EnsureDirectory(string path)
    {
      path = Path.Combine(m_RootDirectory, path);

      if (!Directory.Exists(path))
        Directory.CreateDirectory(path);
    }

    private static void DeleteDirectory(string path)
    {
      path = Path.Combine(m_RootDirectory, path);

      if (Directory.Exists(path))
        Directory.Delete(path, true);
    }

    private static StreamWriter GetWriter(string root, string name) => new StreamWriter(Path.Combine(Path.Combine(m_RootDirectory, root), name));

    private static StreamWriter GetWriter(string path) => new StreamWriter(Path.Combine(m_RootDirectory, path));

    private static readonly string[,] m_Aliases =
    {
      { "System.Object", "<font color=\"blue\">object</font>" },
      { "System.String", "<font color=\"blue\">string</font>" },
      { "System.Boolean", "<font color=\"blue\">bool</font>" },
      { "System.Byte", "<font color=\"blue\">byte</font>" },
      { "System.SByte", "<font color=\"blue\">sbyte</font>" },
      { "System.Int16", "<font color=\"blue\">short</font>" },
      { "System.UInt16", "<font color=\"blue\">ushort</font>" },
      { "System.Int32", "<font color=\"blue\">int</font>" },
      { "System.UInt32", "<font color=\"blue\">uint</font>" },
      { "System.Int64", "<font color=\"blue\">long</font>" },
      { "System.UInt64", "<font color=\"blue\">ulong</font>" },
      { "System.Single", "<font color=\"blue\">float</font>" },
      { "System.Double", "<font color=\"blue\">double</font>" },
      { "System.Decimal", "<font color=\"blue\">decimal</font>" },
      { "System.Char", "<font color=\"blue\">char</font>" },
      { "System.Void", "<font color=\"blue\">void</font>" }
    };

    private static readonly int m_AliasLength = m_Aliases.GetLength(0);

    public static string GetPair(Type varType, string name, bool ignoreRef)
    {
      string prepend = "";
      StringBuilder append = new StringBuilder();

      Type realType = varType;

      if (varType.IsByRef)
      {
        if (!ignoreRef)
          prepend = RefString;

        realType = varType.GetElementType();
      }

      if (realType?.IsPointer == true)
      {
        if (realType.IsArray)
        {
          append.Append('*');

          do
          {
            append.Append('[');

            for (int i = 1; i < realType.GetArrayRank(); ++i)
              append.Append(',');

            append.Append(']');

            realType = realType.GetElementType();
          } while (realType?.IsArray == true);

          append.Append(' ');
        }
        else
        {
          realType = realType.GetElementType();
          append.Append(" *");
        }
      }
      else if (realType?.IsArray == true)
      {
        do
        {
          append.Append('[');

          for (int i = 1; i < realType.GetArrayRank(); ++i)
            append.Append(',');

          append.Append(']');

          realType = realType.GetElementType();
        } while (realType?.IsArray == true);

        append.Append(' ');
      }
      else
      {
        append.Append(' ');
      }

      string fullName = realType?.FullName ?? "(-null-)";
      string aliased = null; // = realType.Name;

      if (realType != null && m_Types.TryGetValue(realType, out TypeInfo info))
      {
        aliased = $"<!-- DBG-0 -->{info.LinkName(null)}";
      }
      else
      {
        if (realType?.IsGenericType == true)
        {
          FormatGeneric(realType, out _, out _, out string linkName);
          aliased = linkName.Replace("@directory@", null);
        }
        else
        {
          for (int i = 0; i < m_AliasLength; ++i)
            if (m_Aliases[i, 0] == fullName)
            {
              aliased = m_Aliases[i, 1];
              break;
            }
        }

        aliased ??= realType?.Name ?? "";
      }

      return string.Concat(prepend, aliased, append, name);
    }

    private static bool Document()
    {
      try
      {
        DeleteDirectory("docs/");
      }
      catch
      {
        return false;
      }

      EnsureDirectory("docs/");
      EnsureDirectory("docs/namespaces/");
      EnsureDirectory("docs/types/");
      EnsureDirectory("docs/bods/");

      GenerateStyles();
      GenerateIndex();

      DocumentCommands();
      DocumentKeywords();
      DocumentBodies();


      m_Types = new Dictionary<Type, TypeInfo>();
      m_Namespaces = new Dictionary<string, List<TypeInfo>>();

      List<Assembly> assemblies = new List<Assembly> { Core.Assembly };

      foreach (Assembly asm in AssemblyHandler.Assemblies)
        assemblies.Add(asm);

      Assembly[] asms = assemblies.ToArray();

      for (int i = 0; i < asms.Length; ++i)
        LoadTypes(asms[i], asms);

      DocumentLoadedTypes();
      DocumentConstructibleObjects();

      return true;
    }

    private static void AddIndexLink(StreamWriter html, string filePath, string label, string desc)
    {
      html.WriteLine("      <h2><a href=\"{0}\" title=\"{1}\">{2}</a></h2>", filePath, desc, label);
    }

    private static void GenerateStyles()
    {
      using StreamWriter css = GetWriter("docs/", "styles.css");
      css.WriteLine("body { background-color: #FFFFFF; font-family: verdana, arial; font-size: 11px; }");
      css.WriteLine("a { color: #28435E; }");
      css.WriteLine("a:hover { color: #4878A9; }");
      css.WriteLine("td.header { background-color: #9696AA; font-weight: bold; font-size: 12px; }");
      css.WriteLine("td.lentry { background-color: #D7D7EB; width: 10%; }");
      css.WriteLine("td.rentry { background-color: #FFFFFF; width: 90%; }");
      css.WriteLine("td.entry { background-color: #FFFFFF; }");
      css.WriteLine("td { font-size: 11px; }");
      css.WriteLine(".tbl-border { background-color: #46465A; }");

      css.WriteLine("td.ir {{ background-color: #{0:X6}; }}", Iron);
      css.WriteLine("td.du {{ background-color: #{0:X6}; }}", DullCopper);
      css.WriteLine("td.sh {{ background-color: #{0:X6}; }}", ShadowIron);
      css.WriteLine("td.co {{ background-color: #{0:X6}; }}", Copper);
      css.WriteLine("td.br {{ background-color: #{0:X6}; }}", Bronze);
      css.WriteLine("td.go {{ background-color: #{0:X6}; }}", Gold);
      css.WriteLine("td.ag {{ background-color: #{0:X6}; }}", Agapite);
      css.WriteLine("td.ve {{ background-color: #{0:X6}; }}", Verite);
      css.WriteLine("td.va {{ background-color: #{0:X6}; }}", Valorite);

      css.WriteLine("td.cl {{ background-color: #{0:X6}; }}", Cloth);
      css.WriteLine("td.pl {{ background-color: #{0:X6};  }}", Plain);
      css.WriteLine("td.sp {{ background-color: #{0:X6}; }}", Core.AOS ? SpinedAOS : SpinedLBR);
      css.WriteLine("td.ho {{ background-color: #{0:X6}; }}", Core.AOS ? HornedAOS : HornedLBR);
      css.WriteLine("td.ba {{ background-color: #{0:X6}; }}", Core.AOS ? BarbedAOS : BarbedLBR);
    }

    private static void GenerateIndex()
    {
      using StreamWriter html = GetWriter("docs/", "index.html");
      html.WriteLine("<html>");
      html.WriteLine("   <head>");
      html.WriteLine("      <title>RunUO Documentation - Index</title>");
      html.WriteLine("      <link rel=\"stylesheet\" type=\"text/css\" href=\"styles.css\" />");
      html.WriteLine("   </head>");
      html.WriteLine("   <body>");

      AddIndexLink(html, "commands.html", "Commands",
        "Every available command. This contains command name, usage, aliases, and description.");
      AddIndexLink(html, "objects.html", "Constructible Objects",
        "Every constructible item or npc. This contains object name and usage. Hover mouse over parameters to see type description.");
      AddIndexLink(html, "keywords.html", "Speech Keywords",
        "Lists speech keyword numbers and associated match patterns. These are used in some scripts for multi-language matching of client speech.");
      AddIndexLink(html, "bodies.html", "Body List",
        "Every usable body number and name. Table is generated from a UO:3D client datafile. If you do not have UO:3D installed, this may be blank.");
      AddIndexLink(html, "overview.html", "Class Overview",
        "Scripting reference. Contains every class type and contained methods in the core and scripts.");

      html.WriteLine("   </body>");
      html.WriteLine("</html>");
    }

    private const int Iron = 0xCCCCDD;
    private const int DullCopper = 0xAAAAAA;
    private const int ShadowIron = 0x777799;
    private const int Copper = 0xDDCC99;
    private const int Bronze = 0xAA8866;
    private const int Gold = 0xDDCC55;
    private const int Agapite = 0xDDAAAA;
    private const int Verite = 0x99CC77;
    private const int Valorite = 0x88AABB;

    private const int Cloth = 0xDDDDDD;
    private const int Plain = 0xCCAA88;
    private const int SpinedAOS = 0x99BBBB;
    private const int HornedAOS = 0xCC8888;
    private const int BarbedAOS = 0xAABBAA;
    private const int SpinedLBR = 0xAA8833;
    private const int HornedLBR = 0xBBBBAA;
    private const int BarbedLBR = 0xCCAA88;

    public static List<BodyEntry> LoadBodies()
    {
      List<BodyEntry> list = new List<BodyEntry>();

      string path = Path.Combine(Core.BaseDirectory, "Data/models.txt");

      if (File.Exists(path))
      {
        using StreamReader ip = new StreamReader(path);
        string line;

        while ((line = ip.ReadLine()) != null)
        {
          line = line.Trim();

          if (line.Length == 0 || line.StartsWith("#"))
            continue;

          string[] split = line.Split('\t');

          if (split.Length >= 9)
          {
            Body body = Utility.ToInt32(split[0]);
            ModelBodyType type = (ModelBodyType)Utility.ToInt32(split[1]);
            string name = split[8];

            BodyEntry entry = new BodyEntry(body, type, name);

            if (!list.Contains(entry))
              list.Add(entry);
          }
        }
      }

      return list;
    }

    private static void DocumentBodies()
    {
      List<BodyEntry> list = LoadBodies();

      using StreamWriter html = GetWriter("docs/", "bodies.html");
      html.WriteLine("<html>");
      html.WriteLine("   <head>");
      html.WriteLine("      <title>RunUO Documentation - Body List</title>");
      html.WriteLine("      <link rel=\"stylesheet\" type=\"text/css\" href=\"styles.css\" />");
      html.WriteLine("   </head>");
      html.WriteLine("   <body>");
      html.WriteLine("      <a name=\"Top\" />");
      html.WriteLine("      <h4><a href=\"index.html\">Back to the index</a></h4>");

      if (list.Count > 0)
      {
        html.WriteLine("      <h2>Body List</h2>");

        list.Sort(new BodyEntrySorter());

        ModelBodyType lastType = ModelBodyType.Invalid;

        for (int i = 0; i < list.Count; ++i)
        {
          BodyEntry entry = list[i];
          ModelBodyType type = entry.BodyType;

          if (type != lastType)
          {
            if (lastType != ModelBodyType.Invalid)
              html.WriteLine("      </table></td></tr></table><br>");

            lastType = type;

            html.WriteLine("      <a name=\"{0}\" />", type);

            switch (type)
            {
              case ModelBodyType.Monsters:
                html.WriteLine(
                  "      <b>Monsters</b> | <a href=\"#Sea\">Sea</a> | <a href=\"#Animals\">Animals</a> | <a href=\"#Human\">Human</a> | <a href=\"#Equipment\">Equipment</a><br><br>");
                break;
              case ModelBodyType.Sea:
                html.WriteLine(
                  "      <a href=\"#Top\">Monsters</a> | <b>Sea</b> | <a href=\"#Animals\">Animals</a> | <a href=\"#Human\">Human</a> | <a href=\"#Equipment\">Equipment</a><br><br>");
                break;
              case ModelBodyType.Animals:
                html.WriteLine(
                  "      <a href=\"#Top\">Monsters</a> | <a href=\"#Sea\">Sea</a> | <b>Animals</b> | <a href=\"#Human\">Human</a> | <a href=\"#Equipment\">Equipment</a><br><br>");
                break;
              case ModelBodyType.Human:
                html.WriteLine(
                  "      <a href=\"#Top\">Monsters</a> | <a href=\"#Sea\">Sea</a> | <a href=\"#Animals\">Animals</a> | <b>Human</b> | <a href=\"#Equipment\">Equipment</a><br><br>");
                break;
              case ModelBodyType.Equipment:
                html.WriteLine(
                  "      <a href=\"#Top\">Monsters</a> | <a href=\"#Sea\">Sea</a> | <a href=\"#Animals\">Animals</a> | <a href=\"#Human\">Human</a> | <b>Equipment</b><br><br>");
                break;
            }

            html.WriteLine("      <table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\">");
            html.WriteLine("      <tr><td class=\"tbl-border\">");
            html.WriteLine("      <table width=\"100%\" cellpadding=\"4\" cellspacing=\"1\">");
            html.WriteLine("         <tr><td width=\"100%\" colspan=\"2\" class=\"header\">{0}</td></tr>",
              type);
          }

          html.WriteLine("         <tr><td class=\"lentry\">{0}</td><td class=\"rentry\">{1}</td></tr>",
            entry.Body.BodyID, entry.Name);
        }

        html.WriteLine("      </table>");
      }
      else
      {
        html.WriteLine("      This feature requires a UO:3D installation.");
      }

      html.WriteLine("   </body>");
      html.WriteLine("</html>");
    }

    private static void DocumentKeywords()
    {
      List<Dictionary<int, SpeechEntry>> tables = LoadSpeechFile();

      using StreamWriter html = GetWriter("docs/", "keywords.html");
      html.WriteLine("<html>");
      html.WriteLine("   <head>");
      html.WriteLine("      <title>RunUO Documentation - Speech Keywords</title>");
      html.WriteLine("      <link rel=\"stylesheet\" type=\"text/css\" href=\"styles.css\" />");
      html.WriteLine("   </head>");
      html.WriteLine("   <body>");
      html.WriteLine("      <h4><a href=\"index.html\">Back to the index</a></h4>");
      html.WriteLine("      <h2>Speech Keywords</h2>");

      for (int p = 0; p < 1 && p < tables.Count; ++p)
      {
        Dictionary<int, SpeechEntry> table = tables[p];

        html.WriteLine("      <table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\">");
        html.WriteLine("      <tr><td class=\"tbl-border\">");
        html.WriteLine("      <table width=\"100%\" cellpadding=\"4\" cellspacing=\"1\">");
        html.WriteLine("         <tr><td class=\"header\">Number</td><td class=\"header\">Text</td></tr>");

        List<SpeechEntry> list = new List<SpeechEntry>(table.Values);
        list.Sort(new SpeechEntrySorter());

        for (int i = 0; i < list.Count; ++i)
        {
          SpeechEntry entry = list[i];

          html.Write("         <tr><td class=\"lentry\">0x{0:X4}</td><td class=\"rentry\">", entry.Index);

          entry.Strings.Sort(); // ( new EnglishPrioStringSorter() );

          for (int j = 0; j < entry.Strings.Count; ++j)
          {
            if (j > 0)
              html.Write("<br>");

            string v = entry.Strings[j];

            for (int k = 0; k < v.Length; ++k)
            {
              char c = v[k];

              if (c == '<')
                html.Write("&lt;");
              else if (c == '>')
                html.Write("&gt;");
              else if (c == '&')
                html.Write("&amp;");
              else if (c == '"')
                html.Write("&quot;");
              else if (c == '\'')
                html.Write("&apos;");
              else if (c >= 0x20 && c < 0x7F)
                html.Write(c);
              else
                html.Write("&#{0};", (int)c);
            }
          }

          html.WriteLine("</td></tr>");
        }

        html.WriteLine("      </table></td></tr></table>");
      }

      html.WriteLine("   </body>");
      html.WriteLine("</html>");
    }

    private class SpeechEntry
    {
      public SpeechEntry(int index)
      {
        Index = index;
        Strings = new List<string>();
      }

      public int Index { get; }

      public List<string> Strings { get; }
    }

    private class SpeechEntrySorter : IComparer<SpeechEntry>
    {
      public int Compare(SpeechEntry x, SpeechEntry y)
      {
        if (x == null && y == null) return 0;
        return x?.Index.CompareTo(y?.Index) ?? 1;
      }
    }

    private static List<Dictionary<int, SpeechEntry>> LoadSpeechFile()
    {
      List<Dictionary<int, SpeechEntry>> tables = new List<Dictionary<int, SpeechEntry>>();
      int lastIndex = -1;

      Dictionary<int, SpeechEntry> table = null;

      string path = Core.FindDataFile("speech.mul", false);

      if (File.Exists(path))
      {
        using FileStream ip = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        BinaryReader bin = new BinaryReader(ip);

        while (bin.PeekChar() >= 0)
        {
          int index = bin.ReadByte() << 8 | bin.ReadByte();
          int length = bin.ReadByte() << 8 | bin.ReadByte();
          string text = Encoding.UTF8.GetString(bin.ReadBytes(length)).Trim();

          if (text.Length == 0)
            continue;

          if (table == null || lastIndex > index)
          {
            if (index == 0 && text == "*withdraw*")
              tables.Insert(0, table = new Dictionary<int, SpeechEntry>());
            else
              tables.Add(table = new Dictionary<int, SpeechEntry>());
          }

          lastIndex = index;

          if (!table.TryGetValue(index, out SpeechEntry entry))
            table[index] = entry = new SpeechEntry(index);

          entry.Strings.Add(text);
        }
      }

      return tables;
    }

    public class DocCommandEntry
    {
      public DocCommandEntry(AccessLevel accessLevel, string name, string[] aliases, string usage, string description)
      {
        AccessLevel = accessLevel;
        Name = name;
        Aliases = aliases;
        Usage = usage;
        Description = description;
      }

      public AccessLevel AccessLevel { get; }

      public string Name { get; }

      public string[] Aliases { get; }

      public string Usage { get; }

      public string Description { get; }
    }

    public class CommandEntrySorter : IComparer<DocCommandEntry>
    {
      public int Compare(DocCommandEntry a, DocCommandEntry b)
      {
        if (a == null && b == null) return 0;

        int v = b?.AccessLevel.CompareTo(a?.AccessLevel) ?? 1;

        if (v != 0)
          return v;

        return a?.Name.CompareTo(b?.Name) ?? 1;
      }
    }

    private static void DocumentCommands()
    {
      using StreamWriter html = GetWriter("docs/", "commands.html");
      html.WriteLine("<html>");
      html.WriteLine("   <head>");
      html.WriteLine("      <title>RunUO Documentation - Commands</title>");
      html.WriteLine("      <link rel=\"stylesheet\" type=\"text/css\" href=\"styles.css\" />");
      html.WriteLine("   </head>");
      html.WriteLine("   <body>");
      html.WriteLine("      <a name=\"Top\" />");
      html.WriteLine("      <h4><a href=\"index.html\">Back to the index</a></h4>");
      html.WriteLine("      <h2>Commands</h2>");

      List<CommandEntry> commands = new List<CommandEntry>(CommandSystem.Entries.Values);
      List<DocCommandEntry> list = new List<DocCommandEntry>();

      commands.Sort();
      commands.Reverse();
      Clean(commands);

      for (int i = 0; i < commands.Count; ++i)
      {
        CommandEntry e = commands[i];

        MethodInfo mi = e.Handler.Method;

        object[] attrs = mi.GetCustomAttributes(typeof(UsageAttribute), false);

        if (attrs.Length == 0)
          continue;

        UsageAttribute usage = attrs[0] as UsageAttribute;

        attrs = mi.GetCustomAttributes(typeof(DescriptionAttribute), false);

        if (attrs.Length == 0)
          continue;

        if (usage == null || !(attrs[0] is DescriptionAttribute desc))
          continue;

        attrs = mi.GetCustomAttributes(typeof(AliasesAttribute), false);

        AliasesAttribute aliases = attrs.Length == 0 ? null : attrs[0] as AliasesAttribute;

        string descString = desc.Description.Replace("<", "&lt;").Replace(">", "&gt;");

        if (aliases == null)
          list.Add(new DocCommandEntry(e.AccessLevel, e.Command, null, usage.Usage, descString));
        else
          list.Add(new DocCommandEntry(e.AccessLevel, e.Command, aliases.Aliases, usage.Usage, descString));
      }

      for (int i = 0; i < TargetCommands.AllCommands.Count; ++i)
      {
        BaseCommand command = TargetCommands.AllCommands[i];

        string usage = command.Usage;
        string desc = command.Description;

        if (usage == null || desc == null)
          continue;

        string[] cmds = command.Commands;
        string cmd = cmds[0];
        string[] aliases = new string[cmds.Length - 1];

        for (int j = 0; j < aliases.Length; ++j)
          aliases[j] = cmds[j + 1];

        desc = desc.Replace("<", "&lt;").Replace(">", "&gt;");

        if (command.Supports != CommandSupport.Single)
        {
          StringBuilder sb = new StringBuilder(50 + desc.Length);

          sb.Append("Modifiers: ");

          if ((command.Supports & CommandSupport.Global) != 0)
            sb.Append("<i><a href=\"#Global\">Global</a></i>, ");

          if ((command.Supports & CommandSupport.Online) != 0)
            sb.Append("<i><a href=\"#Online\">Online</a></i>, ");

          if ((command.Supports & CommandSupport.Region) != 0)
            sb.Append("<i><a href=\"#Region\">Region</a></i>, ");

          if ((command.Supports & CommandSupport.Contained) != 0)
            sb.Append("<i><a href=\"#Contained\">Contained</a></i>, ");

          if ((command.Supports & CommandSupport.Multi) != 0)
            sb.Append("<i><a href=\"#Multi\">Multi</a></i>, ");

          if ((command.Supports & CommandSupport.Area) != 0)
            sb.Append("<i><a href=\"#Area\">Area</a></i>, ");

          if ((command.Supports & CommandSupport.Self) != 0)
            sb.Append("<i><a href=\"#Self\">Self</a></i>, ");

          sb.Remove(sb.Length - 2, 2);
          sb.Append("<br>");
          sb.Append(desc);

          desc = sb.ToString();
        }

        list.Add(new DocCommandEntry(command.AccessLevel, cmd, aliases, usage, desc));
      }

      List<BaseCommandImplementor> commandImpls = BaseCommandImplementor.Implementors;

      for (int i = 0; i < commandImpls.Count; ++i)
      {
        BaseCommandImplementor command = commandImpls[i];

        string usage = command.Usage;
        string desc = command.Description;

        if (usage == null || desc == null)
          continue;

        string[] cmds = command.Accessors;
        string cmd = cmds[0];
        string[] aliases = new string[cmds.Length - 1];

        for (int j = 0; j < aliases.Length; ++j)
          aliases[j] = cmds[j + 1];

        desc = desc.Replace("<", "&lt;").Replace(">", "&gt;");

        list.Add(new DocCommandEntry(command.AccessLevel, cmd, aliases, usage, desc));
      }

      list.Sort(new CommandEntrySorter());

      AccessLevel last = AccessLevel.Player;

      foreach (DocCommandEntry e in list)
      {
        if (e.AccessLevel != last)
        {
          if (last != AccessLevel.Player)
            html.WriteLine("      </table></td></tr></table><br>");

          last = e.AccessLevel;

          html.WriteLine("      <a name=\"{0}\" />", last);

          switch (last)
          {
            case AccessLevel.Administrator:
              html.WriteLine(
                "      <b>Administrator</b> | <a href=\"#GameMaster\">Game Master</a> | <a href=\"#Counselor\">Counselor</a> | <a href=\"#Player\">Player</a><br><br>");
              break;
            case AccessLevel.GameMaster:
              html.WriteLine(
                "      <a href=\"#Top\">Administrator</a> | <b>Game Master</b> | <a href=\"#Counselor\">Counselor</a> | <a href=\"#Player\">Player</a><br><br>");
              break;
            case AccessLevel.Seer:
              html.WriteLine(
                "      <a href=\"#Top\">Administrator</a> | <a href=\"#GameMaster\">Game Master</a> | <a href=\"#Counselor\">Counselor</a> | <a href=\"#Player\">Player</a><br><br>");
              break;
            case AccessLevel.Counselor:
              html.WriteLine(
                "      <a href=\"#Top\">Administrator</a> | <a href=\"#GameMaster\">Game Master</a> | <b>Counselor</b> | <a href=\"#Player\">Player</a><br><br>");
              break;
            case AccessLevel.Player:
              html.WriteLine(
                "      <a href=\"#Top\">Administrator</a> | <a href=\"#GameMaster\">Game Master</a> | <a href=\"#Counselor\">Counselor</a> | <b>Player</b><br><br>");
              break;
          }

          html.WriteLine("      <table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\">");
          html.WriteLine("      <tr><td class=\"tbl-border\">");
          html.WriteLine("      <table width=\"100%\" cellpadding=\"4\" cellspacing=\"1\">");
          html.WriteLine("         <tr><td colspan=\"2\" width=\"100%\" class=\"header\">{0}</td></tr>",
            last == AccessLevel.GameMaster ? "Game Master" : last.ToString());
        }

        DocumentCommand(html, e);
      }

      html.WriteLine("      </table></td></tr></table>");
      html.WriteLine("   </body>");
      html.WriteLine("</html>");
    }

    public static void Clean(List<CommandEntry> list)
    {
      for (int i = 0; i < list.Count; ++i)
      {
        CommandEntry e = list[i];

        for (int j = i + 1; j < list.Count; ++j)
        {
          CommandEntry c = list[j];

          if (e.Handler.Method == c.Handler.Method)
          {
            list.RemoveAt(j);
            --j;
          }
        }
      }
    }

    private static void DocumentCommand(StreamWriter html, DocCommandEntry e)
    {
      string usage = e.Usage;
      string desc = e.Description;
      string[] aliases = e.Aliases;

      html.Write("         <tr><a name=\"{0}\" /><td class=\"lentry\">{0}</td>", e.Name);

      if (aliases == null || aliases.Length == 0)
      {
        html.Write("<td class=\"rentry\"><b>Usage: {0}</b><br>{1}</td>",
          usage.Replace("<", "&lt;").Replace(">", "&gt;"), desc);
      }
      else
      {
        html.Write("<td class=\"rentry\"><b>Usage: {0}</b><br>Alias{1}: ",
          usage.Replace("<", "&lt;").Replace(">", "&gt;"), aliases.Length == 1 ? "" : "es");

        for (int i = 0; i < aliases.Length; ++i)
        {
          if (i != 0)
            html.Write(", ");

          html.Write(aliases[i]);
        }

        html.Write("<br>{0}</td>", desc);
      }

      html.WriteLine("</tr>");
    }

    private static readonly Type typeofItem = typeof(Item);
    private static readonly Type typeofMobile = typeof(Mobile);
    private static readonly Type typeofMap = typeof(Map);
    private static readonly Type typeofCustomEnum = typeof(CustomEnumAttribute);

    private static bool IsConstructible(Type t, out bool isItem) => (isItem = typeofItem.IsAssignableFrom(t)) || typeofMobile.IsAssignableFrom(t);

    private static bool IsConstructible(ConstructorInfo ctor) => ctor.IsDefined(typeof(ConstructibleAttribute), false);

    private static void DocumentConstructibleObjects()
    {
      List<TypeInfo> types = new List<TypeInfo>(m_Types.Values);
      types.Sort(new TypeComparer());

      List<(Type, ConstructorInfo[])> items = new List<(Type, ConstructorInfo[])>();
      List<(Type, ConstructorInfo[])> mobiles = new List<(Type, ConstructorInfo[])>();

      for (int i = 0; i < types.Count; ++i)
      {
        Type t = types[i].m_Type;

        if (t.IsAbstract || !IsConstructible(t, out bool isItem))
          continue;

        ConstructorInfo[] ctors = t.GetConstructors();
        bool anyConstructible = false;

        for (int j = 0; !anyConstructible && j < ctors.Length; ++j)
          anyConstructible = IsConstructible(ctors[j]);

        if (anyConstructible) (isItem ? items : mobiles).Add((t, ctors));
      }

      using StreamWriter html = GetWriter("docs/", "objects.html");
      html.WriteLine("<html>");
      html.WriteLine("   <head>");
      html.WriteLine("      <title>RunUO Documentation - Constructible Objects</title>");
      html.WriteLine("      <link rel=\"stylesheet\" type=\"text/css\" href=\"styles.css\" />");
      html.WriteLine("   </head>");
      html.WriteLine("   <body>");
      html.WriteLine("      <h4><a href=\"index.html\">Back to the index</a></h4>");
      html.WriteLine(
        "      <h2>Constructible <a href=\"#items\">Items</a> and <a href=\"#mobiles\">Mobiles</a></h2>");

      html.WriteLine("      <a name=\"items\" />");
      html.WriteLine("      <table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\">");
      html.WriteLine("      <tr><td class=\"tbl-border\">");
      html.WriteLine("      <table width=\"100%\" cellpadding=\"4\" cellspacing=\"1\">");
      html.WriteLine("         <tr><td class=\"header\">Item Name</td><td class=\"header\">Usage</td></tr>");

      items.ForEach(tuple =>
      {
        var (type, constructors) = tuple;
        DocumentConstructibleObject(html, type, constructors);
      });

      html.WriteLine("      </table></td></tr></table><br><br>");

      html.WriteLine("      <a name=\"mobiles\" />");
      html.WriteLine("      <table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\">");
      html.WriteLine("      <tr><td class=\"tbl-border\">");
      html.WriteLine("      <table width=\"100%\" cellpadding=\"4\" cellspacing=\"1\">");
      html.WriteLine("         <tr><td class=\"header\">Mobile Name</td><td class=\"header\">Usage</td></tr>");

      mobiles.ForEach(tuple =>
      {
        var (type, constructors) = tuple;
        DocumentConstructibleObject(html, type, constructors);
      });

      html.WriteLine("      </table></td></tr></table>");

      html.WriteLine("   </body>");
      html.WriteLine("</html>");
    }

    private static void DocumentConstructibleObject(StreamWriter html, Type t, ConstructorInfo[] ctors)
    {
      html.Write("         <tr><td class=\"lentry\">{0}</td><td class=\"rentry\">", t.Name);

      bool first = true;

      for (int i = 0; i < ctors.Length; ++i)
      {
        ConstructorInfo ctor = ctors[i];

        if (!IsConstructible(ctor))
          continue;

        if (!first)
          html.Write("<br>");

        first = false;

        html.Write("{0}Add {1}", CommandSystem.Prefix, t.Name);

        ParameterInfo[] parms = ctor.GetParameters();

        for (int j = 0; j < parms.Length; ++j)
        {
          html.Write(" <a ");

          if (m_Types.TryGetValue(parms[j].ParameterType, out TypeInfo typeInfo))
            html.Write("href=\"types/{0}\" ", typeInfo.FileName);

          html.Write("title=\"{0}\">{1}</a>", GetTooltipFor(parms[j]), parms[j].Name);
        }
      }

      html.WriteLine("</td></tr>");
    }

    private const string HtmlNewLine = "&#13;";

    private static readonly object[,] m_Tooltips =
    {
      { typeof(byte), "Numeric value in the range from 0 to 255, inclusive." },
      { typeof(sbyte), "Numeric value in the range from negative 128 to positive 127, inclusive." },
      { typeof(ushort), "Numeric value in the range from 0 to 65,535, inclusive." },
      { typeof(short), "Numeric value in the range from negative 32,768 to positive 32,767, inclusive." },
      { typeof(uint), "Numeric value in the range from 0 to 4,294,967,295, inclusive." },
      { typeof(int), "Numeric value in the range from negative 2,147,483,648 to positive 2,147,483,647, inclusive." },
      { typeof(ulong), "Numeric value in the range from 0 through about 10^20." },
      { typeof(long), "Numeric value in the approximate range from negative 10^19 through 10^19." },
      {
        typeof(string),
        "Text value. To specify a value containing spaces, encapsulate the value in quote characters:{0}{0}&quot;Spaced text example&quot;"
      },
      { typeof(bool), "Boolean value which can be either True or False." },
      { typeof(Map), "Map or facet name. Possible values include:{0}{0}- Felucca{0}- Trammel{0}- Ilshenar{0}- Malas" },
      {
        typeof(Poison),
        "Poison name or level. Possible values include:{0}{0}- Lesser{0}- Regular{0}- Greater{0}- Deadly{0}- Lethal"
      },
      {
        typeof(Point3D),
        "Three-dimensional coordinate value. Format as follows:{0}{0}&quot;(<x value>, <y value>, <z value>)&quot;"
      }
    };

    private static string GetTooltipFor(ParameterInfo param)
    {
      Type paramType = param.ParameterType;

      for (int i = 0; i < m_Tooltips.GetLength(0); ++i)
      {
        Type checkType = (Type)m_Tooltips[i, 0];

        if (paramType == checkType)
          return string.Format((string)m_Tooltips[i, 1], HtmlNewLine);
      }

      if (paramType.IsEnum)
      {
        StringBuilder sb = new StringBuilder();

        sb.AppendFormat("Enumeration value or name. Possible named values include:{0}", HtmlNewLine);

        string[] names = Enum.GetNames(paramType);

        for (int i = 0; i < names.Length; ++i)
          sb.AppendFormat("{0}- {1}", HtmlNewLine, names[i]);

        return sb.ToString();
      }

      if (paramType.IsDefined(typeofCustomEnum, false))
      {
        object[] attributes = paramType.GetCustomAttributes(typeofCustomEnum, false);

        if (attributes.Length > 0 && attributes[0] is CustomEnumAttribute attr)
        {
          StringBuilder sb = new StringBuilder();

          sb.AppendFormat("Enumeration value or name. Possible named values include:{0}", HtmlNewLine);

          string[] names = attr.Names;

          for (int i = 0; i < names.Length; ++i)
            sb.AppendFormat("{0}- {1}", HtmlNewLine, names[i]);

          return sb.ToString();
        }
      }
      else if (paramType == typeofMap)
      {
        StringBuilder sb = new StringBuilder();

        sb.AppendFormat("Enumeration value or name. Possible named values include:{0}", HtmlNewLine);

        string[] names = Map.GetMapNames();

        for (int i = 0; i < names.Length; ++i)
          sb.AppendFormat("{0}- {1}", HtmlNewLine, names[i]);

        return sb.ToString();
      }

      return "";
    }

    private const string RefString = "<font color=\"blue\">ref</font> ";
    private const string GetString = " <font color=\"blue\">get</font>;";
    private const string SetString = " <font color=\"blue\">set</font>;";

    private const string InString = "<font color=\"blue\">in</font> ";
    private const string OutString = "<font color=\"blue\">out</font> ";

    private const string VirtString = "<font color=\"blue\">virtual</font> ";
    private const string CtorString = "(<font color=\"blue\">ctor</font>) ";
    private const string StaticString = "(<font color=\"blue\">static</font>) ";

    private static void WriteEnum(TypeInfo info, StreamWriter typeHtml)
    {
      Type type = info.m_Type;

      typeHtml.WriteLine("      <h2>{0} (Enum)</h2>", info.TypeName);

      string[] names = Enum.GetNames(type);

      bool flags = type.IsDefined(typeof(FlagsAttribute), false);
      string format;

      if (flags)
        format = "      {0:G} = 0x{1:X}{2}<br>";
      else
        format = "      {0:G} = {1:D}{2}<br>";

      for (int i = 0; i < names.Length; ++i)
      {
        object value = Enum.Parse(type, names[i]);

        typeHtml.WriteLine(format, names[i], value, i < names.Length - 1 ? "," : "");
      }
    }

    private static void WriteType(TypeInfo info, StreamWriter typeHtml)
    {
      Type type = info.m_Type;

      typeHtml.Write("      <h2>");

      Type decType = info.m_Declaring;

      if (decType != null)
      {
        // We are a nested type

        typeHtml.Write('(');

        m_Types.TryGetValue(decType, out TypeInfo decInfo);

        if (decInfo == null)
          typeHtml.Write(decType.Name);
        else
          // typeHtml.Write( "<a href=\"{0}\">{1}</a>", decInfo.m_FileName, decInfo.m_TypeName );
          typeHtml.Write(decInfo.LinkName(null));

        typeHtml.Write(") - ");
      }

      typeHtml.Write(info.TypeName);

      Type[] ifaces = info.m_Interfaces;
      Type baseType = info.m_BaseType;

      int extendCount = 0;

      if (baseType != typeof(object) && baseType != typeof(ValueType) && baseType?.IsPrimitive == false)
      {
        typeHtml.Write(" : ");

        m_Types.TryGetValue(baseType, out TypeInfo baseInfo);

        if (baseInfo == null)
          typeHtml.Write(baseType.Name);
        else
          typeHtml.Write($"<!-- DBG-1 -->{baseInfo.LinkName(null)}");

        ++extendCount;
      }

      if (ifaces.Length > 0)
      {
        if (extendCount == 0)
          typeHtml.Write(" : ");

        for (int i = 0; i < ifaces.Length; ++i)
        {
          Type iface = ifaces[i];
          m_Types.TryGetValue(iface, out TypeInfo ifaceInfo);

          if (extendCount != 0)
            typeHtml.Write(", ");

          ++extendCount;

          if (ifaceInfo == null)
          {
            FormatGeneric(iface, out _, out _, out string linkName);
            typeHtml.Write($"<!-- DBG-2.1 -->{linkName.Replace("@directory@", null)}");
          }
          else
          {
            typeHtml.Write($"<!-- DBG-2.2 -->{ifaceInfo.LinkName(null)}");
          }
        }
      }

      typeHtml.WriteLine("</h2>");

      List<TypeInfo> derived = info.m_Derived;

      if (derived != null)
      {
        typeHtml.Write("<h4>Derived Types: ");

        derived.Sort(new TypeComparer());

        for (int i = 0; i < derived.Count; ++i)
        {
          TypeInfo derivedInfo = derived[i];

          if (i != 0)
            typeHtml.Write(", ");

          // typeHtml.Write( "<a href=\"{0}\">{1}</a>", derivedInfo.m_FileName, derivedInfo.m_TypeName );
          typeHtml.Write($"<!-- DBG-3 -->{derivedInfo.LinkName(null)}");
        }

        typeHtml.WriteLine("</h4>");
      }

      List<TypeInfo> nested = info.m_Nested;

      if (nested != null)
      {
        typeHtml.Write("<h4>Nested Types: ");

        nested.Sort(new TypeComparer());

        for (int i = 0; i < nested.Count; ++i)
        {
          TypeInfo nestedInfo = nested[i];

          if (i != 0)
            typeHtml.Write(", ");

          // typeHtml.Write( "<a href=\"{0}\">{1}</a>", nestedInfo.m_FileName, nestedInfo.m_TypeName );
          typeHtml.Write($"<!-- DBG-4 -->{nestedInfo.LinkName(null)}");
        }

        typeHtml.WriteLine("</h4>");
      }

      MemberInfo[] membs = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                           BindingFlags.Instance | BindingFlags.DeclaredOnly);

      Array.Sort(membs, new MemberComparer());

      for (int i = 0; i < membs.Length; ++i)
      {
        MemberInfo mi = membs[i];

        if (mi is PropertyInfo propertyInfo)
          WriteProperty(propertyInfo, typeHtml);
        else if (mi is ConstructorInfo constructorInfo)
          WriteCtor(info.TypeName, constructorInfo, typeHtml);
        else if (mi is MethodInfo methodInfo)
          WriteMethod(methodInfo, typeHtml);
      }
    }

    private static void WriteProperty(PropertyInfo pi, StreamWriter html)
    {
      html.Write("      ");

      MethodInfo getMethod = pi.GetGetMethod();
      MethodInfo setMethod = pi.GetSetMethod();

      if (getMethod?.IsStatic == true || setMethod?.IsStatic == true)
        html.Write(StaticString);

      html.Write(GetPair(pi.PropertyType, pi.Name, false));
      html.Write('(');

      if (pi.CanRead)
        html.Write(GetString);

      if (pi.CanWrite)
        html.Write(SetString);

      html.WriteLine(" )<br>");
    }

    private static void WriteCtor(string name, ConstructorInfo ctor, StreamWriter html)
    {
      if (ctor.IsStatic)
        return;

      html.Write("      ");
      html.Write(CtorString);
      html.Write(name);
      html.Write('(');

      ParameterInfo[] parms = ctor.GetParameters();

      if (parms.Length > 0)
      {
        html.Write(' ');

        for (int i = 0; i < parms.Length; ++i)
        {
          ParameterInfo pi = parms[i];

          if (i != 0)
            html.Write(", ");

          if (pi.IsIn)
            html.Write(InString);
          else if (pi.IsOut)
            html.Write(OutString);

          html.Write(GetPair(pi.ParameterType, pi.Name, pi.IsOut));
        }

        html.Write(' ');
      }

      html.WriteLine(")<br>");
    }

    private static void WriteMethod(MethodInfo mi, StreamWriter html)
    {
      if (mi.IsSpecialName)
        return;

      html.Write("      ");

      if (mi.IsStatic)
        html.Write(StaticString);

      if (mi.IsVirtual)
        html.Write(VirtString);

      html.Write(GetPair(mi.ReturnType, mi.Name, false));
      html.Write('(');

      ParameterInfo[] parms = mi.GetParameters();

      if (parms.Length > 0)
      {
        html.Write(' ');

        for (int i = 0; i < parms.Length; ++i)
        {
          ParameterInfo pi = parms[i];

          if (i != 0)
            html.Write(", ");

          if (pi.IsIn)
            html.Write(InString);
          else if (pi.IsOut)
            html.Write(OutString);

          html.Write(GetPair(pi.ParameterType, pi.Name, pi.IsOut));
        }

        html.Write(' ');
      }

      html.WriteLine(")<br>");
    }
  }

  public enum ModelBodyType
  {
    Invalid = -1,
    Monsters,
    Sea,
    Animals,
    Human,
    Equipment
  }

  public class BodyEntry
  {
    public BodyEntry(Body body, ModelBodyType bodyType, string name)
    {
      Body = body;
      BodyType = bodyType;
      Name = name;
    }

    public Body Body { get; }

    public ModelBodyType BodyType { get; }

    public string Name { get; }

    public override bool Equals(object obj)
    {
      BodyEntry e = obj as BodyEntry;

      return Body == e?.Body && BodyType == e.BodyType && Name == e.Name;
    }

    public override int GetHashCode() => Body.BodyID ^ (int)BodyType ^ Name.GetHashCode();
  }

  public class BodyEntrySorter : IComparer<BodyEntry>
  {
    public int Compare(BodyEntry a, BodyEntry b)
    {
      if (a == null && b == null) return 0;
      int v = a?.BodyType.CompareTo(b?.BodyType) ?? 1;

      if (v == 0)
        v = a?.Body.BodyID.CompareTo(b?.Body.BodyID) ?? 1;

      if (v != 0)
        return v;

      return a?.Name.CompareTo(b?.Name) ?? 1;
    }
  }
}
