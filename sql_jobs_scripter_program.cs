using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Collections.Specialized;
using Microsoft.SqlServer.Management.Smo;
using SMO = Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Smo.Agent;
using Microsoft.SqlServer.Management.Common;
using System.IO;
using CommandLine;
using CommandLine.Text;


namespace sql_jobs_scripter
{
    public sealed class Options : CommandLineOptionsBase
    {
        [Option("S", "server", Required = true, HelpText = "Server to connect to")]
        public string Server { get; set; }

        [Option("d", "dir", HelpText = "Output directory")]
        public string out_directory { get; set; }

        [Option("p", "prefix", HelpText = "Job name prefix string")]
        public string prefix { get; set; }

        [Option("1", "1_output_file", HelpText = "single_output_file")]
        public bool single_output_file { get; set; }

        [Option("m", "multi_file", HelpText = "Multiple output files - the default")]
        public bool multiple_output_files { get; set; }

        [Option("o", "output_file", HelpText = "Name of the output file for the single file output")]
        public string output_file_name { get; set; }

    }


    class sql_jobs_scripter_program
    {
       
        static void Main(string[] args)

        {
            bool produce_multi_file = true;
            bool produce_single_file = false;

            var options = new Options();
            var parser = new CommandLineParser(new CommandLineParserSettings(Console.Error));

            if (!parser.ParseArguments(args, options)) Environment.Exit(1);

            
            String job_name_prefix = Properties.Settings.Default.job_name_prefix.ToUpper();
            String output_directory = Properties.Settings.Default.output_dir;
            String server_name = Properties.Settings.Default.Server;

            // override with the command line switches
            if (options.Server != null) server_name = options.Server;
            if (options.out_directory != null) output_directory = options.out_directory;
            if (options.prefix != null) job_name_prefix = options.prefix;

            // make sure the prefix from the arguments is picked up.
            String multi_file_output_name = job_name_prefix + " all jobs.sql";
            if (options.output_file_name != null) multi_file_output_name = options.output_file_name;
            
            produce_single_file = options.single_output_file;
            produce_multi_file = options.multiple_output_files;

            StringCollection sc = new StringCollection();
            ScriptingOptions so = new ScriptingOptions();
            so.IncludeDatabaseContext = true;
           
            //Setup connection, this is windows authentication
            ServerConnection conn = new ServerConnection();
            conn.LoginSecure = true;
            conn.ServerInstance = server_name;

            Server srv = new Server(conn);

            String all_jobs = "";
            String new_script = "";
            String script = "";
            String JobName = null;
            String drop_by_name = null;
            String job_script = "";
            String full_file_name;

            //Loop over all the jobs
            foreach (Job J in srv.JobServer.Jobs)
            {
                JobName = J.Name.ToString();

                if (JobName.ToUpper().StartsWith(job_name_prefix.ToUpper()))
                {
                    //Output name in the console
                    Console.WriteLine(J.Name.ToString());

                    sc = J.Script(so);

                    drop_by_name =@"
IF  EXISTS (SELECT job_id FROM msdb.dbo.sysjobs_view WHERE name = N'" + JobName + @"') begin
EXEC msdb.dbo.sp_delete_job @job_name=N'" + JobName + @"'
      , @delete_unused_schedule=1
      print '" + JobName + @"'
end

";

                    all_jobs += sc[0] + "\r\ngo\r\n\r\n";
                    job_script = sc[1];

                    Int32 pos = job_script.IndexOf("QuitWithRollback\r\n\r\nEND\r\n");

                    new_script = job_script.Substring(0, pos + 25)
                                 + drop_by_name
                                 + job_script.Substring(pos + 25);
                    all_jobs += new_script;

                    //Get all the text for the job
                    foreach (string s in sc)
                    {
                        script += s;
                    }

                    if (produce_multi_file)
                    {
                        //Generate the file
                        full_file_name = System.IO.Path.Combine(output_directory, J.Name.ToString() + ".sql");

                        TextWriter tw = new StreamWriter(full_file_name);
                        tw.Write(script);
                        tw.Close();
                    }

                    //Make the string blank again
                    script = "";
                }

            }

            if (produce_single_file)
            {
                full_file_name = System.IO.Path.Combine(output_directory, multi_file_output_name);
                TextWriter tw2 = new StreamWriter(full_file_name);
                tw2.Write((all_jobs));
                tw2.Close();
            }


        }
 
    }
}