namespace ItgManiaManager.Service
{
    public class PackService : IPackService
    {
        public void UniformDifficulty(string pathToPack, Enum EnumDifficulties)
        {
     
            
            var difficulties = new List<string> { "Beginner", "Easy", "Medium", "Hard", "Challenge" };
            difficulties.Remove(EnumDifficulties.ToString());

            if (string.IsNullOrWhiteSpace(pathToPack))
            {
                throw new Exception("Seleziona un pack");
            }

            var songDirectories = Directory.GetDirectories(pathToPack, "*", SearchOption.AllDirectories);

            foreach (var songdir in songDirectories)
            {
                var songFiles = Directory.GetFiles(songdir, "*", SearchOption.AllDirectories);
                foreach (var file in songFiles)
                {
                    if (file.EndsWith(".ssc", StringComparison.OrdinalIgnoreCase))
                    {
                        // --- FILE .SSC ---
                        string contenuto = File.ReadAllText(file);
                        bool modificato = false;

                        foreach (var difficulty in difficulties)
                        {
                            if (contenuto.Contains($"#DIFFICULTY:{difficulty}"))
                            {
                                contenuto = contenuto.Replace($"#DIFFICULTY:{difficulty}", $"#DIFFICULTY:{EnumDifficulties}");
                                modificato = true;
                                Console.WriteLine($"{file} from {difficulty} to {EnumDifficulties}");
                            }
                        }

                        if (modificato)
                        {
                            File.WriteAllText(file, contenuto);
                            Console.WriteLine($"Salvato {file}\n");
                        }
                    }
                    else if (file.EndsWith(".sm", StringComparison.OrdinalIgnoreCase))
                    {
                        // --- FILE .SM ---
                        var righe = File.ReadAllLines(file);
                        bool modificato = false;

                        for (int i = 0; i < righe.Length; i++)
                        {
                            if (righe[i].Trim() == "#NOTES:")
                            {
                                // riga difficulty = i + 3
                                int diffIndex = i + 3;
                                if (diffIndex < righe.Length)
                                {
                                    string vecchia = righe[diffIndex].TrimEnd(':').Trim();
                                    if (difficulties.Contains(vecchia))
                                    {
                                        righe[diffIndex] = $"     {EnumDifficulties}:";
                                        modificato = true;
                                        Console.WriteLine($"{file} from {vecchia} to {EnumDifficulties}");
                                    }
                                }
                            }
                        }

                        if (modificato)
                        {
                            File.WriteAllLines(file, righe);
                            Console.WriteLine($"Salvato {file}\n");
                        }
                    }
                }
            }

            Console.WriteLine(" Tutte le modifiche completate!");
        }
    }
}

