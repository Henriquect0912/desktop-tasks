# Hoje Widget — especificação executável da V0.1 para Windows

> Este documento é a fonte de verdade da V0.1. Implemente exatamente o que está descrito aqui. Não amplie o produto, não antecipe versões futuras e não substitua requisitos difíceis por aproximações silenciosas.

## 1. Instrução ao Claude Code

Você deve construir, testar e entregar um aplicativo pessoal de tarefas para Windows chamado provisoriamente **Hoje Widget**.

O resultado deve ser uma aplicação realmente utilizável, não apenas um protótipo visual ou um projeto parcialmente montado. Trabalhe até que o projeto compile, os testes passem, o executável seja gerado e o fluxo principal tenha sido verificado no Windows.

Regras de execução:

1. Antes de alterar qualquer coisa, inspecione o diretório atual e preserve arquivos e mudanças que já existirem.
2. Se o diretório estiver vazio, crie a solução conforme a arquitetura definida neste documento.
3. Não faça novas perguntas de produto: as decisões funcionais estão fechadas aqui.
4. Se encontrar uma decisão técnica menor não especificada, escolha a alternativa mais simples e robusta que preserve os requisitos e registre-a no `README.md`.
5. Não troque o comportamento de widget da área de trabalho por uma janela sempre no topo. O modo de área de trabalho é requisito central.
6. Não implemente funcionalidades listadas como fora do escopo, mesmo que pareçam rápidas.
7. Use textos da interface em português do Brasil.
8. Ao terminar, execute build, testes e publicação local. Informe de forma objetiva onde está o executável e como iniciar/parar o aplicativo.

## 2. Objetivo do produto

Construir o núcleo de um futuro aplicativo pessoal de produtividade. Nesta versão, sua única função é:

> Mostrar o que preciso fazer hoje e permitir que eu dê baixa nessas tarefas conforme avanço no dia.

O aplicativo é um widget minimalista na área de trabalho do Windows, com dados exclusivamente locais. Ele deve iniciar com o Windows, lembrar sua posição e tamanho em diferentes configurações de monitores e permanecer atrás dos aplicativos normais, como um widget de celular colocado sobre o papel de parede.

## 3. Escopo congelado

Entra obrigatoriamente na V0.1:

- widget interativo na área de trabalho do Windows;
- adicionar uma tarefa textual;
- editar e excluir uma tarefa;
- marcar e desmarcar uma tarefa;
- manter concluídas riscadas e atenuadas até a mudança de dia;
- mover automaticamente tarefas concluídas para a parte inferior;
- preservar indefinidamente tarefas não concluídas;
- arrastar tarefas pendentes para reordená-las manualmente;
- mover e redimensionar o widget;
- salvar posição e tamanho por configuração de monitores;
- impedir que o widget abra fora da área visível;
- rolagem vertical quando a lista não couber;
- quebra de linha para textos longos;
- dois temas: Preto e Translúcido;
- ícone e menu na bandeja do Windows;
- inicialização automática com o Windows, ativada por padrão;
- atalho global fixo `Ctrl + Shift + Q`;
- aviso de revisão uma vez por dia;
- persistência local em SQLite;
- execução em instância única.

Qualquer recurso não necessário para cumprir essa lista deve ser evitado.

## 4. Plataforma e arquitetura obrigatórias

### 4.1 Stack

Use:

- **C# com .NET 8**;
- **WPF** para a interface;
- padrão **MVVM** simples, sem framework pesado de MVVM;
- **Microsoft.Data.Sqlite** para persistência;
- P/Invoke isolado em uma camada de interoperabilidade Windows para WorkerW/Progman, hotkey global, monitoramento de telas e demais APIs Win32 necessárias;
- `System.Windows.Forms.NotifyIcon` para a bandeja, habilitando Windows Forms no projeto WPF se necessário;
- testes com **xUnit**.

Não use Electron, navegador embutido, servidor local, serviço web ou runtime JavaScript. O aplicativo é exclusivamente Windows e o comportamento de janela depende de APIs nativas do sistema.

### 4.2 Estrutura sugerida

Mantenha a solução pequena e legível, aproximadamente assim:

```text
HojeWidget.sln
src/
  HojeWidget/
    App.xaml
    Views/
    ViewModels/
    Models/
    Services/
      Database/
      DesktopHost/
      DisplayLayout/
      GlobalHotkey/
      Startup/
      Tray/
    Interop/
tests/
  HojeWidget.Tests/
README.md
```

Não é necessário reproduzir exatamente essas pastas se uma organização equivalente ficar mais clara. Evite abstrações especulativas para recursos futuros.

### 4.3 Requisitos gerais de engenharia

- Habilitar nullable reference types.
- Separar regras de domínio da interface para que possam ser testadas.
- Usar operações SQLite parametrizadas e transações nas alterações de ordenação.
- Criar e migrar o banco automaticamente na inicialização.
- Tratar falhas sem corromper o banco nem perder todas as configurações.
- Não exigir privilégios de administrador.
- Ser compatível com Windows 10 e Windows 11 de 64 bits.
- Declarar suporte a DPI por monitor (`PerMonitorV2`) no manifesto.
- Não mostrar console junto com a aplicação publicada.
- Usar uma única instância. Uma segunda execução deve sinalizar a instância existente para mostrar o widget, e então terminar.

## 5. Modelo visual

### 5.1 Janela principal

Tamanho inicial: **410 × 300 pixels independentes de DPI**.

Tamanho mínimo recomendado: **300 × 220**. O tamanho máximo é limitado à área útil do monitor atual.

A janela deve ter:

- nenhuma barra de título tradicional;
- nenhuma borda visível do Windows;
- nenhum botão padrão de minimizar, maximizar ou fechar;
- cantos discretamente arredondados;
- texto branco ou quase branco;
- espaçamento confortável e visual minimalista;
- ausência na barra de tarefas e no `Alt + Tab`;
- cabeçalho `HOJE`;
- botão `+` no canto superior direito;
- rodapé com a quantidade de tarefas pendentes;
- cabeçalho funcionando como alça para mover a janela;
- redimensionamento pelas extremidades e cantos, embora a borda seja invisível.

Referência conceitual:

```text
HOJE                                      +

☐ Fazer lista de Termodinâmica

☐ Preparar aula do CASD

☐ Comprar comida para a semana

☐ Academia

4 pendentes
```

Não adicionar logotipo grande, barra lateral, navegação por páginas, calendário ou tela principal separada.

### 5.2 Temas

Existem somente dois temas.

**Preto**

- fundo preto ou quase preto, visualmente sólido;
- texto principal claro;
- texto secundário cinza;
- estados de foco e hover discretos.

**Translúcido**

- fundo preto/cinza parcialmente transparente, aproximadamente 80% a 85% opaco;
- texto com contraste suficiente em papéis de parede claros e escuros;
- pode usar blur/acrílico se isso não comprometer estabilidade, mas transparência legível é o requisito, não um efeito específico.

Não incluir seletor de cor, slider de opacidade, tema claro, tema do sistema ou temas personalizados. O tema escolhido deve persistir após reiniciar.

## 6. Estados e interações da lista

### 6.1 Tarefa

Na V0.1, uma tarefa contém somente o texto e os metadados internos necessários a estado, ordem e persistência. Para o usuário não existem data, prazo, prioridade, categoria, observação ou subtarefa.

O texto:

- é obrigatório após remover espaços do início e do fim;
- deve aceitar Unicode e acentos;
- deve quebrar em múltiplas linhas;
- não pode escapar horizontalmente do widget;
- deve ter um limite defensivo de 500 caracteres;
- não permite criar uma tarefa vazia.

### 6.2 Adicionar

Ao clicar em `+`, mostrar um campo dentro do próprio widget com o placeholder:

```text
O que você precisa fazer?
```

Comportamento:

- o campo recebe foco imediatamente;
- `Enter` salva;
- `Esc` cancela;
- texto vazio não salva;
- a nova tarefa é inserida ao final das tarefas pendentes e antes das concluídas;
- não abrir diálogo ou janela separada;
- não mostrar formulário adicional.

### 6.3 Concluir e desmarcar

Ao marcar uma tarefa:

- salvar a alteração imediatamente;
- mostrar a checkbox marcada;
- riscar o texto;
- atenuar visualmente a linha;
- mover a tarefa para o grupo inferior de concluídas;
- manter a tarefa visível durante o restante daquele dia.

Ao clicar novamente na checkbox no mesmo dia:

- voltar ao estado pendente;
- remover risco e atenuação;
- retornar a tarefa ao grupo de pendentes;
- conservar sua posição manual relativa anterior sempre que possível.

As pendentes são exibidas primeiro, por ordem manual. As concluídas de hoje são exibidas depois, em ordem de conclusão. Não precisa haver título explícito separando os grupos.

### 6.4 Ordenação manual

- permitir drag-and-drop para reordenar tarefas pendentes;
- mostrar feedback visual claro do ponto de inserção;
- salvar a nova ordem imediatamente e de forma atômica;
- restaurar exatamente essa ordem após reiniciar;
- não permitir que o usuário coloque uma concluída no meio das pendentes;
- não aplicar qualquer ordenação automática por texto, criação ou prioridade.

### 6.5 Editar e excluir

Ao passar o mouse sobre uma tarefa, mostrar discretamente `⋯` no fim da linha. Ao clicar, oferecer somente:

```text
Editar
Excluir
```

Editar:

- converte o texto em um campo no próprio item;
- `Enter` salva e `Esc` cancela;
- aplica as mesmas regras de validação da criação.

Excluir:

- abre uma confirmação pequena com o texto `Excluir esta tarefa?`;
- ações `Excluir` e `Cancelar`;
- `Esc` cancela;
- após confirmação, remove definitivamente a tarefa;
- não existe lixeira, desfazer, histórico ou arquivamento.

### 6.6 Contagem

O rodapé conta somente tarefas pendentes e usa singular/plural corretamente:

```text
1 pendente
4 pendentes
```

## 7. Estados vazios e conclusão do dia

### 7.1 Nenhuma tarefa

Se não houver tarefa pendente nem tarefa concluída hoje, mostrar:

```text
HOJE                                      +

Nada por enquanto.

+ Adicionar primeira tarefa
```

`+ Adicionar primeira tarefa` inicia o mesmo fluxo de criação. Não mostrar “Tudo concluído” nesse estado.

### 7.2 Tudo concluído

Se existirem tarefas concluídas hoje e nenhuma pendente, destacar acima da lista:

```text
✓

Tudo concluído por hoje.

Você terminou suas tarefas.
```

As tarefas concluídas permanecem listadas discretamente abaixo dessa mensagem até a mudança de dia.

## 8. Regra de mudança de dia

Use a data civil local do Windows, no formato lógico `YYYY-MM-DD`, e não um intervalo fixo de 24 horas.

Regra central:

> Pendência não expira. Conclusão expira visualmente no dia seguinte.

Ao iniciar o aplicativo, ao retomar o computador da suspensão, ao receber mudança relevante do relógio e enquanto o processo estiver aberto:

- recalcular a data local atual;
- manter todas as tarefas pendentes;
- exibir somente tarefas concluídas cuja data local de conclusão seja hoje;
- esconder concluídas em datas anteriores;
- atualizar o estado vazio, a contagem e o aviso diário.

Não é obrigatório apagar fisicamente tarefas concluídas antigas do SQLite. Elas podem continuar armazenadas para integridade interna, mas não pode existir interface de histórico na V0.1.

A mudança de dia deve ocorrer sem exigir que o usuário reinicie o aplicativo. Uma verificação leve a cada minuto é aceitável, além dos eventos de retomada/alteração de hora.

## 9. Revisão diária

Na primeira ativação do aplicativo em cada data local, mostrar dentro do widget um aviso compacto:

```text
Bom dia.

Revise suas tarefas de hoje.

+ Adicionar tarefa        Tudo certo
```

Regras:

- o aviso aparece no máximo uma vez por data;
- `+ Adicionar tarefa` abre o campo de criação sem sair do widget;
- `Tudo certo` fecha o aviso e grava a confirmação daquela data;
- se o computador ou aplicativo reiniciar, o aviso não reaparece se já foi confirmado;
- na primeira ativação de uma nova data, ele volta a aparecer;
- não usar notificação toast do Windows, som ou lembrete repetido;
- o aviso não impede o uso das tarefas existentes.

Considere “ativação” como a primeira inicialização/mostra do widget naquela data. Se o widget iniciar oculto por alguma recuperação de estado, apresente o aviso na primeira vez que ele for mostrado.

## 10. Comportamento de janela na área de trabalho

### 10.1 Modo normal: Área de trabalho

Este é o modo padrão. O widget deve ficar:

- visualmente acima do papel de parede e junto aos elementos da área de trabalho;
- atrás de Chrome, Word, VS Code e qualquer outro aplicativo normal;
- interativo quando a área de trabalho estiver acessível;
- fora da barra de tarefas e do alternador `Alt + Tab`;
- visível ao usar “Mostrar área de trabalho”.

Implemente a hospedagem no shell do Windows localizando a janela apropriada `WorkerW`/`Progman` e associando o HWND do widget a ela. Isole essa lógica em um serviço que consiga:

- anexar o widget ao host da área de trabalho;
- preservar coordenadas e DPI ao anexar/desanexar;
- detectar reinicialização do Explorer e anexar novamente;
- validar que o HWND pai resultante é o host correto;
- nunca deixar o widget permanentemente `Topmost` nesse modo.

O truque de WorkerW varia entre versões do Windows. Implemente-o defensivamente e documente no README a estratégia utilizada. Não declare o requisito atendido apenas colocando uma janela normal com `Topmost = false`.

### 10.2 Modo temporário: Primeiro plano

O atalho global fixo **`Ctrl + Shift + Q`** alterna entre os dois modos:

1. se estiver no modo Área de trabalho, desanexar preservando a posição, mostrar em primeiro plano, aplicar `Topmost` e dar foco ao widget;
2. se estiver no modo Primeiro plano, remover `Topmost`, devolver o widget ao host da área de trabalho e preservar a posição;
3. se estiver oculto, o primeiro acionamento deve mostrá-lo diretamente no modo Primeiro plano;
4. clicar fora não fecha, não oculta e não devolve automaticamente o widget à área de trabalho;
5. somente novo `Ctrl + Shift + Q`, ação da bandeja ou encerramento altera esse modo.

Registrar a hotkey global com `RegisterHotKey`. Se a combinação estiver ocupada, não trocar silenciosamente por outra: registrar o erro, manter o restante do app funcional e mostrar uma mensagem única e clara por meio do ícone da bandeja. Não criar configuração de atalho.

## 11. Bandeja do Windows

O processo mantém um ícone próximo ao relógio.

Clique simples no ícone:

- se o widget estiver visível, ocultá-lo sem encerrar o processo;
- se estiver oculto, mostrá-lo no modo Área de trabalho em sua posição salva.

Menu de contexto com botão direito:

```text
Mostrar/Ocultar
────────────
Tema
   Preto
   Translúcido
────────────
Iniciar com o Windows ✓
────────────
Sair
```

Regras:

- o rótulo pode ser `Mostrar` ou `Ocultar` conforme o estado atual;
- o tema selecionado aparece marcado e muda imediatamente;
- `Iniciar com o Windows` aparece marcado/desmarcado conforme o estado real;
- `Sair` encerra de fato o processo, libera a hotkey, remove o ícone e fecha o banco;
- ocultar o widget não encerra o aplicativo;
- não construir uma tela separada de Configurações.

## 12. Inicialização com o Windows

- Ativada por padrão na primeira execução.
- Pode ser desativada e reativada pelo menu da bandeja.
- Implementar por usuário, sem privilégios administrativos, usando `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run` ou mecanismo equivalente simples e reversível.
- Usar o caminho absoluto e corretamente escapado do executável publicado.
- Ao desativar, remover somente a entrada pertencente ao Hoje Widget.
- Em testes automatizados, abstrair esse acesso e não alterar a inicialização real da máquina.

## 13. Multi-monitor, posição e tamanho

O caso principal do usuário alterna entre:

- notebook de 17 polegadas sozinho;
- notebook de 17 polegadas mais monitor externo de 24 polegadas.

O aplicativo deve guardar um layout independente para cada **configuração de monitores**.

Exemplo:

```text
CONFIGURAÇÃO A — notebook + monitor externo
Widget no monitor externo, canto superior direito

CONFIGURAÇÃO B — somente notebook
Widget no notebook, em outra posição
```

Ao reconectar o monitor externo, restaurar o layout da configuração A. Ao removê-lo, restaurar o layout da configuração B.

### 13.1 Assinatura da configuração

Crie uma assinatura determinística dos monitores conectados, ordenados de forma estável, usando sempre que disponível:

- identificador/nome do dispositivo;
- limites e área útil;
- resolução;
- escala/DPI;
- indicação de monitor principal.

Posição e tamanho devem ser tratados em unidades coerentes com DPI por monitor. Evite misturar pixels físicos e DIPs sem conversão explícita.

### 13.2 Dados salvos por assinatura

Salvar:

- coordenada X;
- coordenada Y;
- largura;
- altura;
- monitor de destino;
- assinatura da configuração;
- data da última atualização.

Persistir alterações de movimento/redimensionamento com debounce curto, por exemplo 300–500 ms, e também no encerramento normal.

### 13.3 Segurança de posicionamento

O widget jamais pode abrir completamente fora da tela.

Ao restaurar:

1. localizar o layout da assinatura atual;
2. validar tamanho e interseção com a área útil de um monitor conectado;
3. limitar o tamanho se exceder a área útil;
4. ajustar as coordenadas para manter cabeçalho e parte suficiente do widget acessíveis;
5. se não houver layout válido, usar o monitor principal, no canto superior direito, com margem aproximada de 24 DIPs;
6. ao mudar a topologia de monitores durante a execução, trocar para o layout correspondente e salvar o layout anterior sem sobrescrevê-lo.

Use eventos de mudança de exibição do Windows e faça uma validação adicional ao retomar da suspensão.

## 14. Rolagem e textos longos

- A janela nunca cresce automaticamente por causa da quantidade de tarefas.
- A área central da lista deve ter rolagem vertical quando necessário.
- A barra de rolagem deve ser discreta, mas utilizável.
- O cabeçalho e o rodapé permanecem acessíveis.
- Textos longos quebram linha e aumentam apenas a altura do item.
- Não criar rolagem horizontal.

## 15. Persistência local

### 15.1 Local dos dados

Guardar dados por usuário em:

```text
%LOCALAPPDATA%\HojeWidget\app.db
```

Se houver logs técnicos, use:

```text
%LOCALAPPDATA%\HojeWidget\logs\
```

Não gravar dados mutáveis em `Program Files` nem ao lado do executável publicado.

### 15.2 Esquema mínimo sugerido

Use migrações versionadas e, no mínimo, estruturas equivalentes a:

```sql
CREATE TABLE tasks (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    is_completed INTEGER NOT NULL DEFAULT 0,
    completed_local_date TEXT NULL,
    completed_at_utc TEXT NULL,
    sort_order INTEGER NOT NULL,
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL
);

CREATE TABLE settings (
    key TEXT PRIMARY KEY,
    value_json TEXT NOT NULL
);

CREATE TABLE display_layouts (
    display_signature TEXT PRIMARY KEY,
    monitor_device TEXT NOT NULL,
    x_dip REAL NOT NULL,
    y_dip REAL NOT NULL,
    width_dip REAL NOT NULL,
    height_dip REAL NOT NULL,
    updated_at_utc TEXT NOT NULL
);

CREATE TABLE daily_review (
    local_date TEXT PRIMARY KEY,
    dismissed_at_utc TEXT NOT NULL
);

CREATE TABLE schema_info (
    version INTEGER NOT NULL
);
```

O esquema pode variar tecnicamente, desde que preserve os comportamentos e permita migrações futuras.

### 15.3 Regras de consistência

- Cada edição relevante persiste imediatamente; não depender apenas do fechamento do app.
- Uma falha de UI não pode deixar ordens duplicadas ou inconsistentes.
- Ao concluir, preencher estado, data local e instante UTC.
- Ao desmarcar, limpar os campos de conclusão.
- Normalizar `sort_order` quando necessário dentro de uma transação.
- Criar backup recuperável antes de uma migração destrutiva futura; nenhuma migração destrutiva é necessária na V0.1.
- Não enviar nem sincronizar qualquer dado.

## 16. Ciclo de vida e robustez

- Fechar/ocultar a janela pelo fluxo da bandeja não deve matar o processo, exceto em `Sair`.
- Restaurar corretamente após suspensão, mudança de DPI, mudança de resolução e conexão/remoção de monitor.
- Reanexar ao desktop após reinicialização do Explorer.
- Liberar handles, eventos, mutex, ícone e registro de hotkey ao sair.
- Não travar se o banco estiver temporariamente bloqueado; fazer poucas tentativas curtas e apresentar erro claro se persistir.
- Se o banco não puder ser aberto, não sobrescrevê-lo. Registrar o erro e informar o usuário de forma curta.
- Logs devem ser locais, pequenos, rotativos e sem conteúdo desnecessário das tarefas. Não existe telemetria.

## 17. Fora do escopo — proibição explícita

Não implementar nesta versão:

- calendário;
- listas “Amanhã”, “Esta semana” ou “Algum dia”;
- tarefas futuras;
- datas limite ou dias planejados;
- horários e duração estimada;
- prioridades manuais ou automáticas;
- categorias, etiquetas ou áreas da vida;
- campos especiais para ITA, CASD, academia, provas ou relatórios;
- recorrências e rotinas;
- subtarefas;
- projetos;
- dependências entre tarefas;
- planejamento ou reordenação automática;
- notificações, lembretes, alarmes ou sons;
- Pomodoro;
- estatísticas, métricas ou gamificação;
- histórico visível, lixeira, arquivo ou restauração de excluídas;
- inteligência artificial;
- sugestões automáticas;
- integração com Google Calendar ou qualquer outro serviço;
- servidor, API, internet ou sincronização;
- conta, login ou nuvem;
- telemetria;
- tela completa do aplicativo;
- tela de configurações;
- atalhos configuráveis;
- mais de dois temas;
- modo compacto;
- atualização automática;
- instalador complexo ou distribuição pela Microsoft Store.

Não inclua estruturas de interface vazias, botões desabilitados ou modelos de dados “para depois”.

## 18. Critérios de aceite funcionais

Considere a V0.1 aceita somente se todos os itens abaixo forem verdadeiros em uma máquina Windows compatível.

### Tarefas

- [ ] Criar uma tarefa com `+` e `Enter` funciona sem abrir outra janela.
- [ ] `Esc` cancela criação e edição.
- [ ] Texto vazio não cria tarefa.
- [ ] Texto longo quebra linha sem rolagem horizontal.
- [ ] Editar persiste depois de reiniciar.
- [ ] Excluir exige confirmação e remove definitivamente.
- [ ] Marcar conclui, risca, atenua e move para baixo.
- [ ] Desmarcar devolve à lista pendente.
- [ ] Arrastar reordena as pendentes e a ordem persiste.
- [ ] O contador considera somente pendentes e trata singular/plural.

### Dia

- [ ] Tarefas pendentes de ontem continuam hoje.
- [ ] Tarefas concluídas hoje permanecem visíveis até o fim do dia.
- [ ] Na mudança de data, concluídas antigas somem sem reiniciar.
- [ ] Lista genuinamente vazia mostra `Nada por enquanto.`.
- [ ] Lista com tudo concluído mostra a mensagem de conclusão e mantém as concluídas abaixo.
- [ ] O aviso de revisão aparece uma vez por data e não reaparece após `Tudo certo`, mesmo reiniciando.

### Janela e sistema

- [ ] No modo normal, o widget fica acima do papel de parede e atrás de aplicativos comuns.
- [ ] O widget não aparece na barra de tarefas nem no `Alt + Tab`.
- [ ] `Ctrl + Shift + Q` o traz para o primeiro plano e dá foco.
- [ ] Pressionar novamente devolve o widget ao desktop.
- [ ] Clicar fora no modo Primeiro plano não o oculta nem altera o modo.
- [ ] O atalho funciona mesmo quando outro aplicativo está focado.
- [ ] Clique simples na bandeja alterna visibilidade sem encerrar.
- [ ] O menu da bandeja troca entre exatamente dois temas.
- [ ] `Sair` encerra o processo de fato.
- [ ] Uma segunda execução não cria um segundo widget.
- [ ] O widget se recupera após reiniciar o Explorer.

### Persistência e monitores

- [ ] Tarefas e configurações sobrevivem ao reinício do app e do Windows.
- [ ] A aplicação funciona sem internet.
- [ ] Com notebook + monitor externo, posição e tamanho são restaurados nessa configuração.
- [ ] Com apenas o notebook, outro layout é restaurado.
- [ ] Ao reconectar o monitor externo, o layout anterior de dois monitores retorna.
- [ ] O widget nunca fica inacessível fora das telas conectadas.
- [ ] Alterar escala/DPI ou resolução não torna o widget inacessível.
- [ ] Tema e opção de iniciar com Windows persistem.

### Escopo e qualidade

- [ ] Nenhum item proibido da seção 17 aparece no produto.
- [ ] O projeto compila em Release sem erros.
- [ ] Todos os testes automatizados passam.
- [ ] O executável publicado abre em Windows 10/11 x64 sem exigir SDK .NET instalado.
- [ ] Não há console, servidor, login, telemetria ou acesso à rede.

## 19. Testes mínimos obrigatórios

Crie testes automatizados para a lógica que não depende diretamente da UI ou do shell:

- validação e normalização de título;
- criação, edição, conclusão e desmarcação;
- agrupamento de pendentes antes de concluídas;
- persistência da ordem manual;
- filtro de concluídas por data local;
- transição de dia;
- singular/plural da contagem;
- estado vazio versus tudo concluído;
- aviso diário uma vez por data;
- geração determinística da assinatura de monitores;
- seleção do layout correto por assinatura;
- ajuste de posição e tamanho para dentro da área útil;
- persistência SQLite em banco temporário;
- abstração da opção de inicialização sem alterar o registro real durante testes.

Faça também verificação manual documentada no `README.md` para os recursos dependentes do Windows:

- WorkerW/Progman e z-order;
- alternância pelo hotkey global;
- tray;
- reinício do Explorer;
- conexão/remoção do monitor externo;
- DPI diferente em dois monitores;
- inicialização com o Windows.

## 20. Entrega e definição de pronto

Antes de declarar conclusão:

1. restaurar dependências;
2. compilar toda a solução em Release;
3. executar todos os testes;
4. corrigir erros e avisos relevantes;
5. publicar para `win-x64` como aplicação self-contained, sem console;
6. iniciar o executável publicado e validar o fluxo principal;
7. conferir que o processo fecha corretamente por `Sair`;
8. registrar no `README.md`:
   - requisitos suportados;
   - como desenvolver e testar;
   - como publicar;
   - caminho do executável gerado;
   - como ativar/desativar início com Windows;
   - onde ficam banco e logs;
   - limitações técnicas reais encontradas, se houver;
   - checklist dos testes manuais efetuados.

Comandos esperados, adaptando apenas nomes de projeto se necessário:

```powershell
dotnet restore
dotnet build HojeWidget.sln -c Release
dotnet test HojeWidget.sln -c Release --no-build
dotnet publish src/HojeWidget/HojeWidget.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

O modo single-file pode ser removido somente se alguma dependência nativa comprovadamente exigir arquivos separados. Nesse caso, mantenha publicação self-contained e explique no README.

Ao finalizar, entregue um resumo curto contendo:

- o que foi implementado;
- resultado de build e testes;
- caminho exato do executável publicado;
- itens manuais ainda não verificáveis automaticamente;
- qualquer desvio desta especificação, que deve ser raro e justificado.

## 21. Prioridade em caso de conflito técnico

Se dois requisitos entrarem em tensão, preserve nesta ordem:

1. dados locais sem perda;
2. widget realmente integrado à área de trabalho e não permanentemente sobre aplicativos;
3. atalho global e recuperação de visibilidade;
4. restauração segura em múltiplos monitores;
5. regras corretas de tarefas e mudança de dia;
6. simplicidade visual;
7. efeitos cosméticos como blur.

Não sacrifique os cinco primeiros itens para obter acabamento visual.
