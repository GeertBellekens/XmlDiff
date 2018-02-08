using System;
using System.IO;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Xml;
using Microsoft.XmlDiffPatch;
using System.Windows.Forms.Html;
using System.Security.Cryptography.Xml;
using System.Xml.Linq;
using System.Linq;

namespace VisualXmlDiff
{
    class XmlComparer
    {
        
        public void compareDirectories (string path1, string path2, string resultPath, XmlDiffOptions diffOptions, bool compareFragments, XmlDiffAlgorithm algorithm)
        {


            //loop files in the directory1
            foreach (var file in new DirectoryInfo(path1).GetFiles("*.xsd"))
            {
                //find corresponding file in second directory
                var file2 = Path.Combine(path2, file.Name);
                if (File.Exists(file2))
                    compareFiles(file.FullName, file2, resultPath, diffOptions, compareFragments, algorithm);
            }


        }
        public void compareFiles(string file1, string file2, string resultPath, XmlDiffOptions diffOptions, bool compareFragments, XmlDiffAlgorithm algorithm)
        {
            // canonicalize files
            file1 = canonicalize(file1);
            file2 = canonicalize(file2);

            //The main class which is used to compare two files.
            XmlDiff diff = new XmlDiff();
            diff.Options = diffOptions;
            diff.Algorithm = algorithm;

            //output diff file.
            string diffFile = resultPath + Path.DirectorySeparatorChar + "vxd.out";
            XmlTextWriter tw = new XmlTextWriter(new StreamWriter(diffFile));
            tw.Formatting = Formatting.Indented;

            bool isEqual = false;
            //Now compare the two files.
            try
            {
                isEqual = diff.Compare(file1, file2, compareFragments, tw);
            }
            catch (XmlException xe)
            {
                MessageBox.Show("An exception occured while comparing\n" + xe.StackTrace);
            }
            finally
            {
                tw.Close();
            }

            if (isEqual)
            {
                //This means the files were identical for given options.
                return; //dont need to show the differences.
            }

            //Files were not equal, so construct XmlDiffView.
            XmlDiffView dv = new XmlDiffView();

            //Load the original file again and the diff file.
            XmlTextReader orig = new XmlTextReader(file1);
            XmlTextReader diffGram = new XmlTextReader(diffFile);
            dv.Load(orig,diffGram);

            //create the HTML output
            createHtmlResult(file1, file2, resultPath, dv);

            //cleanup
            dv = null;
            orig.Close();
            diffGram.Close();
            File.Delete(diffFile);
        }

        private static void createHtmlResult(string file1, string file2, string resultPath, XmlDiffView dv)
        {
            string outputFile = resultPath + Path.DirectorySeparatorChar + new FileInfo(file1).Name + "_compare.html";
            StreamWriter sw1 = new StreamWriter(outputFile);
            //Wrap the HTML file with necessary html and 
            //body tags and prepare it before passing it to the GetHtml method.
            sw1.Write("<html><body><table width='100%'>");
            //Write Legend.
            sw1.Write(@"<tr><td colspan=""2"" align=""center""><b>Legend:</b> <font style=""background-color: yellow"" 
                 color=""black"">added</font>&nbsp;&nbsp;<font style=""background-color: red""
                 color=""black"">removed</font>&nbsp;&nbsp;<font style=""background-color: 
                lightgreen"" color=""black"">changed</font>&nbsp;&nbsp;
                <font style=""background-color: red"" color=""blue"">moved from</font>
                &nbsp;&nbsp;<font style=""background-color: yellow"" color=""blue"">moved to
                </font>&nbsp;&nbsp;<font style=""background-color: white"" color=""#AAAAAA"">
                ignored</font></td></tr>");


            sw1.Write("<tr><td><b> File Name : ");
            sw1.Write(file1);
            sw1.Write("</b></td><td><b> File Name : ");
            sw1.Write(file2);
            sw1.Write("</b></td></tr>");

            //This gets the differences but just has the 
            //rows and columns of an HTML table
            dv.GetHtml(sw1);

            //Finish wrapping up the generated HTML and complete the file.
            sw1.Write("</table></body></html>");
            //HouseKeeping...close everything we dont want to lock.
            sw1.Close();
            //read the file again and change colors
            // red + blue => plum + blue, yellow + blue => LightCyan + blue
            string text = File.ReadAllText(outputFile);
            text = text.Replace("red\" color=\"blue", "Plum\" color=\"blue");
            text = text.Replace("yellow\" color=\"blue", "LightCyan\" color=\"blue");
            File.WriteAllText(outputFile, text);
        }

        private string canonicalize(string file)
        {
            file = orderXmlNodes(file);
            //create c14n instance and load in xml file
            XmlDsigC14NTransform c14n = new XmlDsigC14NTransform(false);
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(file);
            c14n.LoadInput(xmlDoc);

            //get canonalised stream
            Stream s1 = (Stream)c14n.GetOutput(typeof(Stream));
  
            //create new xmldocument and save
            String newFilename = file + ".canonical";
            XmlDocument xmldoc2 = new XmlDocument();
            xmldoc2.Load(s1);
            xmldoc2.Save(newFilename);

            return newFilename;
        }
        private string orderXmlNodes(string file)
        {
            var xdoc = XDocument.Load(file);
            SortElementsInPlace(xdoc.Root);
            string newFileName = file + ".ordered";
            xdoc.Save(newFileName);
            return newFileName;
        }
        private void SortElementsInPlace(XContainer xContainer)
        {
            var orderedElements = (from child in xContainer.Elements()
                                    orderby child.Name.LocalName, child.Attribute("name")?.Value
                                    select child).ToList();  // ToList matters, since we remove all of the child elements next

            xContainer.Elements().Remove();
            xContainer.Add(orderedElements);  
        }
    }
}
