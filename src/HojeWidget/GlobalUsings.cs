// O projeto habilita WPF e Windows Forms ao mesmo tempo, porque a bandeja usa
// NotifyIcon. O conjunto padrao de usings implicitos do Windows Forms importa
// System.Drawing e System.Windows.Forms em todos os arquivos, o que torna
// ambiguos nomes como Application, Point, TextBox e MouseEventArgs.
// Por isso os usings globais sao declarados aqui e cada arquivo importa
// explicitamente o que precisa.

global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
