using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Common
{
    public class FileManager
    {
        private string _requestsPath;

        private string _responsesPath;

        private string _outgoingRequestsPath;

        private string _outgoingResponsesPath;

        private const string _excludedFolder = "excluded";

        public FileManager()
        { }

        public FileManager(string requestsPath, string responsesPath, string outgoingrequestsPath, string outgoingresponsesPath)
        {
            _requestsPath = requestsPath;
            _responsesPath = responsesPath;
            _outgoingRequestsPath = outgoingrequestsPath;
            _outgoingResponsesPath = outgoingresponsesPath;
        }

        protected Dictionary<int, string> GetFilesDictionary(string path)
        {
            if (String.IsNullOrEmpty(path))
                throw new Exception("Не указан путь к файлам");

            if (!Directory.Exists(path))
                throw new Exception(String.Format("Путь '{0}' не найден", path)); 

            Dictionary<int, string> dictionary = new Dictionary<int, string>();

            string[] files = Directory.GetFiles(path);
            foreach (string file in files)
            {
                // получаем идентификаторы из имен файлов, проверяем что нет повторяющихся, иначе - ошибка
                string fileName = Path.GetFileNameWithoutExtension(file);

                Match m = Regex.Match(fileName, @"^\d+");           // ищем числа в начале имени
                int id;
                if (m.Success)
                    id = Int32.Parse(m.Value);
                else
                    continue;   // пропускаем

                if (dictionary.ContainsKey(id))
                    throw new Exception("Обнаружено дублирование идентификаторов.");

                dictionary.Add(id, file);
            }

            return dictionary;
        }

        private string CreateGetListResponse(string path)
        {
            try
            {
                Dictionary<int, string> dictionary = GetFilesDictionary(path);

                if (dictionary.Count == 0)
                    return "0";

                IEnumerable<string> items = dictionary.Keys.Select(key => String.Format("{0},{1},{2}", key, DateTime.Now.ToString(), Guid.NewGuid().ToString()));

                return String.Format("1;{0}", String.Join(";", items));
            }
            catch (Exception ex)
            {
                return "99;" + ex.Message;
            }
        }

        protected string RemoveFiles(string id, string path)
        {
            try
            {
                Dictionary<int, string> dictionary = GetFilesDictionary(path);

                //проверка на дубликаты id
                int eq_id = dictionary.Count(pair => pair.Key == Int32.Parse(id));
                if (eq_id == 1)
                {
                    //поиск в словаре по id
                    string pathfrom = dictionary[Int32.Parse(id)];
                    string fileName = Path.GetFileName(pathfrom);
                    string pathto = Path.Combine(path, _excludedFolder, fileName);

                    if (File.Exists(pathfrom))
                    {
                        if (!File.Exists(pathto))
                        {
                            //Проверка на существование пути
                            if (!Directory.Exists(Path.Combine(path, _excludedFolder)))
                                Directory.CreateDirectory(Path.Combine(path, _excludedFolder));

                            //Перемещаем файл
                            File.Move(pathfrom, pathto);

                        }
                        else
                            throw new Exception("Файл уже существует в каталоге excluded.");
                    }
                    else
                        throw new Exception("Файл не найден.");

                }
                else
                {
                    if (eq_id < 1)
                        throw new Exception("Файл с данным id не найден.");
                    else if (eq_id > 1)
                        throw new Exception("Обнаружено дублирование идентификаторов.");
                }

                return "1";
            }
            catch (Exception ex)
            {
                return "99;" + ex.Message;
            }

        }

        private string GetState()
        {
            try
            {
                //return String.Format("{0};{1},{2}", "2", DateTime.Now.ToString(), Guid.NewGuid().ToString());
                return String.Format("{0};{1},{2}", GetStateFromFile(), DateTime.Now.ToString(), Guid.NewGuid().ToString());
            }
            catch (Exception ex)
            {
                return "99;" + ex.Message;
            }
        }

        public string GetRequestList()
        {
            return CreateGetListResponse(_requestsPath);
        }

        public string GetResponseList()
        {
            return CreateGetListResponse(_responsesPath);
        }

        private string GetDocument(string id, string path)
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

                return "1;" + base64String;
            }
            catch (Exception ex)
            {
                return "99;" + ex.Message;
            }
        }

        public string GetRequest(string id)
        {
            return GetDocument(id, _requestsPath);
        }

        public string GetResponse(string id)
        {
            return GetDocument(id, _responsesPath);
        }

        public string SendRequest(byte[] xml)
        {
            try
            {
                int requestId = GetNextRequestId();

                string fileName = String.Format("{0}-{1}.xml", requestId, DateTime.Now.ToShortDateString());

                File.WriteAllBytes(Path.Combine(_outgoingRequestsPath, fileName), xml);

                return "1;" + requestId;
            }
            catch (Exception ex)
            {
                return "99;" + ex.Message;
            }
        }

        public string GetResponseState(string id)
        {
            return GetState();
        }

        public string SendResponse(byte[] xml)
        {
            try
            {
                int responseId = GetNextResponseId();

                string fileName = String.Format("{0}-{1}.xml", responseId, DateTime.Now.ToShortDateString());

                File.WriteAllBytes(Path.Combine(_outgoingResponsesPath, fileName), xml);

                return "1;" + responseId;
            }
            catch (Exception ex)
            {
                return "99;" + ex.Message;
            }
        }

        private int GetNextResponseId()
        {
            return GetNextId("lastResponseId");
        }

        private int GetNextRequestId()
        {
            return GetNextId("lastRequestId");
        }


        private int GetNextId(string fileName)
        {
            string filePath = Path.Combine(Environment.CurrentDirectory, fileName);

            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "1");
                return 1;
            }

            string idString = File.ReadAllText(filePath);

            int id;
            if (Int32.TryParse(idString, out id))
            {
                id++;
                File.WriteAllText(filePath, id.ToString());
                return id;
            }
            else
            {
                File.WriteAllText(filePath, "1");
                return 1;
            }
        }


        private string GetStateFromFile()
        {
            string filePath = Path.Combine(Environment.CurrentDirectory, "StateFile.txt");

            if (File.Exists(filePath))
            {
                string state = File.ReadAllText(filePath);
                int stateInt;

                if (Int32.TryParse(state, out stateInt))
                {
                    if ((0 <= stateInt) & (stateInt <= 2))
                    {
                        return state;
                    }
                    else return "2";
                }
                else return "2";
            }
            else return "2";          
        }


        public string GetRequestState(string id)
        {
            return GetState();
        }

        public string AckRequest(string id, string state)
        {            
            return RemoveFiles(id, _requestsPath);
        }

        public string AckResponse(string id, string state)
        {
            return RemoveFiles(id, _responsesPath);
        }
    }
}
