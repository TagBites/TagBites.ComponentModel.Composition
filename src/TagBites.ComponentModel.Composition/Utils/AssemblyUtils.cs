using System.Reflection;
using System.Text;

namespace TagBites.Utils
{
    internal static class AssemblyUtils
    {
        public static string GetName(Assembly assembly)
        {
            var name = assembly.FullName;
            var idx = name.IndexOf(',');
            if (idx > 0)
                name = name.Substring(0, idx);
            return name;
        }

        public static string GetTitle(Assembly assembly)
        {
            return assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
        }
        public static string GetDescription(Assembly assembly)
        {
            return assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
        }
        public static string GetProduct(Assembly assembly)
        {
            return assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        }
        public static string GetTrademark(Assembly assembly)
        {
            return assembly.GetCustomAttribute<AssemblyTrademarkAttribute>()?.Trademark;
        }
        public static string GetCopyright(Assembly assembly)
        {
            return assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
        }
        public static string GetCompany(Assembly assembly)
        {
            return assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        }
        public static string GetFullFriendlyName(Assembly assembly)
        {
            var name = assembly.GetName();

            var sb = new StringBuilder();
            sb.Append(FirstNotNullOrEmptyWhiteSpace(GetTitle(assembly), GetProduct(assembly), assembly.IsDynamic ? "Dynamic Library" : name.Name));
            sb.Append(" v. ");
            sb.Append(name.Version);

            return sb.ToString();
        }

        private static string FirstNotNullOrEmptyWhiteSpace(params string[] values)
        {
            foreach (var item in values)
                if (!string.IsNullOrWhiteSpace(item))
                    return item;

            return null;
        }
    }
}
