﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SuperSocket.SocketBase;
using Microsoft.Scripting.Hosting;

namespace SuperSocket.Dlr
{
    /// <summary>
    /// DynamicCommandLoader
    /// </summary>
    public class DynamicCommandLoader : DynamicCommandLoaderBase
    {
        private HashSet<string> m_ScriptFileExtensions;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicCommandLoader"/> class.
        /// </summary>
        public DynamicCommandLoader()
            : base()
        {
            LoadScriptFileExtensions();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicCommandLoader"/> class.
        /// </summary>
        /// <param name="scriptRuntime">The script runtime.</param>
        public DynamicCommandLoader(ScriptRuntime scriptRuntime)
            : base(scriptRuntime)
        {
            LoadScriptFileExtensions();
        }

        private void LoadScriptFileExtensions()
        {
            List<string> fileExtensions = new List<string>();

            foreach (var fxts in ScriptRuntime.Setup.LanguageSetups.Select(s => s.FileExtensions))
                fileExtensions.AddRange(fxts);

            m_ScriptFileExtensions = new HashSet<string>(fileExtensions, StringComparer.OrdinalIgnoreCase);
        }

        private IEnumerable<string> GetCommandFiles(string path, SearchOption option)
        {
            return Directory.GetFiles(path, "*.*", option).Where(f => m_ScriptFileExtensions.Contains(Path.GetExtension(f)));
        }

        /// <summary>
        /// Gets the script sources.
        /// </summary>
        /// <param name="appServer">The app server.</param>
        /// <returns></returns>
        protected override IEnumerable<IScriptSource> GetScriptSources(IAppServer appServer)
        {
            var sources = new List<IScriptSource>();

            var commandDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Command");
            var serverCommandDir = Path.Combine(commandDir, appServer.Name);

            if (!Directory.Exists(commandDir))
            {
                return sources;
            }

            List<string> commandFiles = new List<string>();

            commandFiles.AddRange(GetCommandFiles(commandDir, SearchOption.TopDirectoryOnly));

            if (Directory.Exists(serverCommandDir))
            {
                commandFiles.AddRange(GetCommandFiles(serverCommandDir, SearchOption.TopDirectoryOnly));
            }

            if (!commandFiles.Any())
            {
                return sources;
            }

            foreach (var file in commandFiles)
            {
                sources.Add(new FileScriptSource(file));
            }

            return sources;
        }
    }
}
