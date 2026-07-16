namespace ExploitStrap
{
    public class JsonManager<T> where T : class, new()
    {
        protected T _prop = new();

        public virtual T Prop
        {
            get => _prop;
            set => _prop = value;
        }

        /// <summary>
        /// The file hash when last retrieved from disk
        /// </summary>
        public string? LastFileHash { get; private set; }

        public bool Loaded { get; protected set; } = false;

        public virtual string ClassName { get; }

        public virtual string FileName => $"{ClassName}.json";

        public virtual string FileLocation => Path.Combine(Paths.Base, FileName);

        public bool IsSaved => File.Exists(FileLocation);

        public virtual string LOG_IDENT_CLASS => $"JsonManager<{ClassName}>";

        public JsonManager(string? className = null)
        {
            ClassName = string.IsNullOrEmpty(className) ? typeof(T).Name : className;
        }

        public virtual bool Load(bool alertFailure = true)
        {
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Load";

            App.Logger.WriteLine(LOG_IDENT, $"Loading from {FileLocation}...");

            try
            {
                if (File.Exists(FileLocation))
                {
                    string contents = File.ReadAllText(FileLocation);

                    T? settings = JsonSerializer.Deserialize<T>(contents);

                    if (settings is null)
                        throw new ArgumentNullException("Deserialization returned null");

                    _prop = settings;
                    Loaded = true;
                    LastFileHash = MD5Hash.FromString(contents);

                    App.Logger.WriteLine(LOG_IDENT, "Loaded successfully!");

                    return true;
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Could not find {FileLocation}.");
                    Loaded = true;

                    return false;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to load!");
                App.Logger.WriteException(LOG_IDENT, ex);

                if (alertFailure)
                {
                    string message = "";

                    if (ClassName == nameof(Settings))
                        message = Strings.JsonManager_SettingsLoadFailed;
                    else if (ClassName == nameof(FastFlagManager))
                        message = Strings.JsonManager_FastFlagsLoadFailed;

                    if (!String.IsNullOrEmpty(message))
                        Frontend.ShowMessageBox($"{message}\n\n{ex.Message}", System.Windows.MessageBoxImage.Warning);

                    try
                    {
                        // Create a backup of loaded file
                        File.Copy(FileLocation, FileLocation + ".bak", true);
                    }
                    catch (Exception copyEx)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to create backup file: {FileLocation}.bak");
                        App.Logger.WriteException(LOG_IDENT, copyEx);
                    }
                }

                Loaded = true;
                Save();

                return false;
            }
        }

        public virtual void Save()
        {
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Save";

            App.Logger.WriteLine(LOG_IDENT, $"Saving to {FileLocation}...");

            Directory.CreateDirectory(Path.GetDirectoryName(FileLocation)!);

            try
            {
                // The tray, a launching bootstrapper, and the settings UI can all write this
                // file at once (different processes of the same install). Serialize writers on
                // a per-file named mutex so they don't interleave. Best-effort: if we can't get
                // the lock in time we still write, because the atomic swap below prevents a
                // torn file regardless — the lock only avoids a lost update. Inside the try so
                // even a mutex-creation failure hits the graceful handler below.
                using var ipl = new InterProcessLock("Json-" + MD5Hash.FromString(FileLocation), TimeSpan.FromSeconds(3));

                string contents = JsonSerializer.Serialize(Prop, new JsonSerializerOptions { WriteIndented = true });

                // Write to a unique temp file, then atomically swap it into place. A crash or
                // a concurrent reader never sees a half-written settings.json — the classic
                // cause of "settings reset themselves" / parse-fail-on-next-launch reports.
                string tempPath = FileLocation + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
                try
                {
                    File.WriteAllText(tempPath, contents);
                    File.Move(tempPath, FileLocation, overwrite: true);
                }
                catch
                {
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                    throw;
                }

                LastFileHash = MD5Hash.FromString(contents);

                // Reap any leftover temp files from a prior write that was killed between
                // WriteAllText and Move. Self-healing, so no separate startup sweep is needed —
                // orphans can't outlive the next successful save.
                SweepOrphanedTempFiles();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to save");
                App.Logger.WriteException(LOG_IDENT, ex);

                string errorMessage = string.Format(Resources.Strings.Bootstrapper_JsonManagerSaveFailed, ClassName, ex.Message);
                Frontend.ShowMessageBox(errorMessage, System.Windows.MessageBoxImage.Warning);

                return;
            }

            App.Logger.WriteLine(LOG_IDENT, "Save complete!");
        }

        // Deletes stray "<FileName>.<hex>.tmp" siblings left by a Save() that was killed
        // mid-write (between WriteAllText and the atomic Move). Best-effort and quiet — a
        // temp still being written by a concurrent Save is held open and simply skipped.
        private void SweepOrphanedTempFiles()
        {
            try
            {
                string? dir = Path.GetDirectoryName(FileLocation);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                    return;

                foreach (string temp in Directory.EnumerateFiles(dir, FileName + ".*.tmp"))
                {
                    try { File.Delete(temp); } catch { /* in use by a live write, or already gone */ }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException($"{LOG_IDENT_CLASS}::SweepOrphanedTempFiles", ex);
            }
        }

        public virtual void Delete()
        {
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Delete";

            try
            {
                if (File.Exists(FileLocation))
                {
                    File.Delete(FileLocation);

                    Loaded = false;
                    App.Logger.WriteLine(LOG_IDENT, "Delete complete!");
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, "File does not exist on disk");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to delete");
                App.Logger.WriteException(LOG_IDENT, ex);

                // should we notify?
            }
        }

        /// <summary>
        /// Is the file on disk different to the one deserialised during this session?
        /// </summary>
        public bool HasFileOnDiskChanged()
        {
            // check if a file has been created since launch
            if (string.IsNullOrEmpty(LastFileHash) && File.Exists(FileLocation))
                return true;

            return LastFileHash != MD5Hash.FromFile(FileLocation);
        }
    }

    /// <summary>
    /// <see cref="JsonManager{T}"/> that will automatically load in the JSON if it has not been already
    /// </summary>
    /// <typeparam name="T">Class</typeparam>
    public class LazyJsonManager<T> : JsonManager<T> where T : class, new()
    {
        public override T Prop
        {
            get
            {
                if (!Loaded)
                    Load();

                return _prop;
            }
            set
            {
                _prop = value;
                Loaded = true;
            }
        }

        public LazyJsonManager(string? className)
            : base(className)
        {
        }
    }
}
