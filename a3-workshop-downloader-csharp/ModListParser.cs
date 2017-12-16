using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Collections.Specialized;
using System.Xml.XPath;

namespace ArmaWorkshopUpdater
{
    class ModListParser
    {
        private XPathDocument ModListDocument { get; set; }
        private List<Tuple<string, string>> ModList { get; set; }
        public ModListParser(string uri)
        {
            ModListDocument = new XPathDocument(uri);
            ModList = new List<Tuple<string, string>>();
        }
        public void ParseModList()
        {
            var xml = ModListDocument.CreateNavigator().Select("/html/body/table/tr");
            var name_rows = xml.Current.Select("//td[@data-type='DisplayName']");
            var link_rows = xml.Current.Select("//td/a[@data-type='Link']") ;
            while (name_rows.MoveNext() && link_rows.MoveNext())
            {
                ModList.Add(new Tuple<string, string>(name_rows.Current.Value, link_rows.Current.Value));
            }            
        }
        public List<Tuple<string, string>> Mods()
        {
            return ModList;
        }
    }
}
