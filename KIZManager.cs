using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.IO;


namespace Common
{
    public class KIZManager : FileManager
    {
        private string _requestsPath;

        private string _outgoingResponsesPath;

        private const string _excludedFolder = "excluded";

        public KIZManager()
        {

        }

        public KIZManager(string requestsPath, string outgoingresponsesPath)
        {
            _requestsPath = requestsPath;
            _outgoingResponsesPath = outgoingresponsesPath;
        }

        private string CreateGetListResponse(string path)
        {
            try
            {
              
                List<String> items = new List<String>(); 
                Dictionary<int, string> dictionary = GetFilesDictionary(path);

                if (dictionary.Count == 0)
                    return "0";

                foreach (var key in dictionary.Keys)
                {
                    string value, fulltype;

                    dictionary.TryGetValue(key, out value);
                    string filename = Path.GetFileName(value);
                   
                    if (filename.IndexOf('-') > -1)
                    {
                        string type = value.Substring(value.IndexOf('-') + 1, 1);

                        switch (type)
                        {
                            case "a":
                                fulltype = "accomod_request";
                                break;
                            case "p":
                                fulltype = "present_request";
                                break;
                            default:
                                fulltype = "uknown-type";
                                break;
                        }
                        items.Add(String.Format("{0},{1},{2}", key, DateTime.Now.ToShortDateString(), fulltype));   
                    }
                    else
                        continue;   // пропускаем документ с некорректным форматом       
                }

                //string total = String.Format("1;{0}", String.Join(";", items));
                return String.Format("1:{0}", String.Join(";", items));
            }
            catch (Exception ex)
            {
                return "99;" + ex.Message;
            }
        }

        private string GetDocumentXml(string id, string path)
        {
            if (String.IsNullOrEmpty(path))
                return "99; Не указан путь к файлам";

            if (!Directory.Exists(path))
                return String.Format("99; Путь '{0}' не найден", path);

            try
            {
                var dictionary = GetFilesDictionary(path);

                int key;
                if (!Int32.TryParse(id, out key))
                    return "91; Идентификатор запроса должен быть числом";

                if (!dictionary.ContainsKey(key))
                    return "92; Запрос с идентификатором " + id + " не найден";

                string fileName = dictionary[key];

                string fileText = File.ReadAllText(fileName);

                byte[] buffer = Encoding.UTF8.GetBytes(fileText);
                string base64String = Convert.ToBase64String(buffer);
                //перенос файла в excluded
                RemoveFiles(id, path);

                return "1," + base64String;
            }
            catch (Exception ex)
            {
                return "99;" + ex.Message;
            }
        }

        private string CheckId(string id, string path)
        {

            if (String.IsNullOrEmpty(path))
                return "99; Не указан путь к файлам";

            if (!Directory.Exists(path))
                return String.Format("99; Путь '{0}' не найден", path);

            try
            {
                var dictionary = GetFilesDictionary(path);

                int key;
                if (!Int32.TryParse(id, out key))
                    return "91; Неверный идентификатор - не является целым числом";

                if (dictionary.ContainsKey(key))
                    return "99; Файл с идентификатором " + id + " уже существует";

                return "0";

            }
            catch (Exception ex)
            {
                return "99;" + ex.Message;
            }
        }
      
        public string GetRequestListKIZ()
        {
            return CreateGetListResponse(_requestsPath);
        }

        public string GetRequestXml(string id)
        {
            return GetDocumentXml(id, _requestsPath);           
        }

        public string SendResponse(string responseId, byte[] xml)
        {
            try
            {
                string CheckResult = CheckId(responseId, _outgoingResponsesPath);

                if (CheckResult == "0")
                {
                    string fileName = String.Format("{0}-{1}.xml", responseId, DateTime.Now.ToShortDateString());

                    File.WriteAllBytes(Path.Combine(_outgoingResponsesPath, fileName), xml);

                    return "0";
                }
                else 
                    return CheckResult;

            }
            catch (Exception ex)
            {
                return "99;" + ex.Message;
            }
        }


     

       


    }
}
