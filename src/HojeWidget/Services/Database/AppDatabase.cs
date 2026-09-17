using Microsoft.Data.Sqlite;

namespace HojeWidget.Services.Database;

/// <summary>
/// Conexao SQLite unica do processo, com migracoes versionadas e acesso serializado.
/// Uma unica conexao evita disputa de lock entre threads da UI e temporizadores.
/// </summary>
public sealed class AppDatabase : IDisposable
{
    /// <summary>
    /// 1 = V0.1, 2 = V0.2 (data planejada), 3 = V0.3 (prazo), 4 = V0.4 (rotinas),
    /// 5 = V0.5 (projetos, entregas e subtarefas), 6 = V0.6 (provas e sessões de estudo),
    /// 7 = V0.7 (prioridade, capacidade e plano do dia),
    /// 8 = V0.7.1 (árvore de estudo, grafo de dependências e limites por prova).
    /// Toda migração é aditiva: nenhuma versão apaga dados das anteriores.
    /// </summary>
    private const int TargetSchemaVersion = 8;

    private readonly int _targetVersion;
    private readonly object _gate = new();
    private readonly SqliteConnection _connection;
    private bool _disposed;

    /// <param name="maxVersion">
    /// Para até esta versão de esquema em vez de ir à atual. Existe para os testes
    /// de migração poderem montar um banco antigo de verdade e depois deixar a
    /// migração real rodar em cima dele.
    /// </param>
    public AppDatabase(string databasePath, int? maxVersion = null)
    {
        DatabasePath = databasePath;
        _targetVersion = Math.Min(maxVersion ?? TargetSchemaVersion, TargetSchemaVersion);

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = 5,
        };

        _connection = new SqliteConnection(builder.ToString());
        _connection.Open();

        Execute(connection =>
        {
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
            pragma.ExecuteNonQuery();
            return 0;
        });

        Migrate();
    }

    public string DatabasePath { get; }

    /// <summary>Executa uma operacao com a conexao, serializando o acesso.</summary>
    public T Execute<T>(Func<SqliteConnection, T> action)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return action(_connection);
        }
    }

    public void Execute(Action<SqliteConnection> action)
        => Execute<object?>(connection =>
        {
            action(connection);
            return null;
        });

    /// <summary>Executa dentro de uma transacao. Alteracoes de ordenacao dependem disso.</summary>
    public void InTransaction(Action<SqliteConnection> action)
        => Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            action(connection);
            transaction.Commit();
        });

    private void Migrate() => InTransaction(connection =>
    {
        using (var create = connection.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE IF NOT EXISTS schema_info (
                    version INTEGER NOT NULL
                );
                """;
            create.ExecuteNonQuery();
        }

        var version = 0;
        using (var read = connection.CreateCommand())
        {
            read.CommandText = "SELECT version FROM schema_info LIMIT 1;";
            var value = read.ExecuteScalar();
            if (value is not null && value != DBNull.Value)
            {
                version = Convert.ToInt32(value);
            }
        }

        if (version < 1 && _targetVersion >= 1)
        {
            using var migration = connection.CreateCommand();
            migration.CommandText = """
                CREATE TABLE IF NOT EXISTS tasks (
                    id TEXT PRIMARY KEY,
                    title TEXT NOT NULL,
                    is_completed INTEGER NOT NULL DEFAULT 0,
                    completed_local_date TEXT NULL,
                    completed_at_utc TEXT NULL,
                    sort_order INTEGER NOT NULL,
                    created_at_utc TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS ix_tasks_sort_order ON tasks (sort_order);
                CREATE INDEX IF NOT EXISTS ix_tasks_completed ON tasks (is_completed, completed_local_date);

                CREATE TABLE IF NOT EXISTS settings (
                    key TEXT PRIMARY KEY,
                    value_json TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS display_layouts (
                    display_signature TEXT PRIMARY KEY,
                    monitor_device TEXT NOT NULL,
                    x_dip REAL NOT NULL,
                    y_dip REAL NOT NULL,
                    width_dip REAL NOT NULL,
                    height_dip REAL NOT NULL,
                    updated_at_utc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS daily_review (
                    local_date TEXT PRIMARY KEY,
                    dismissed_at_utc TEXT NOT NULL
                );
                """;
            migration.ExecuteNonQuery();
        }

        if (version < 2 && _targetVersion >= 2)
        {
            // V0.2: data planejada. As tarefas da V0.1 recebem uma data para continuarem
            // visíveis depois do upgrade — a data de conclusão, quando houver, ou hoje.
            // Sem isso elas cairiam em "Algum dia" e sumiriam do widget.
            using var migration = connection.CreateCommand();
            migration.CommandText = """
                ALTER TABLE tasks ADD COLUMN planned_date TEXT NULL;

                UPDATE tasks
                   SET planned_date = COALESCE(completed_local_date, date('now', 'localtime'));

                CREATE INDEX IF NOT EXISTS ix_tasks_planned_date ON tasks (planned_date);
                """;
            migration.ExecuteNonQuery();
        }

        if (version < 3 && _targetVersion >= 3)
        {
            // V0.3: prazo. Independente da data planejada e sempre opcional, então
            // nenhuma tarefa existente precisa ser tocada.
            using var migration = connection.CreateCommand();
            migration.CommandText = """
                ALTER TABLE tasks ADD COLUMN due_date TEXT NULL;
                CREATE INDEX IF NOT EXISTS ix_tasks_due_date ON tasks (due_date);
                """;
            migration.ExecuteNonQuery();
        }

        if (version < 4 && _targetVersion >= 4)
        {
            // V0.4: rotinas. Tabelas novas; tasks não é tocada.
            // A constraint única em (routine_id, occurrence_date) é o que garante
            // que a geração de ocorrências possa rodar quantas vezes for preciso
            // sem nunca duplicar nada.
            using var migration = connection.CreateCommand();
            migration.CommandText = """
                CREATE TABLE IF NOT EXISTS routines (
                    id TEXT PRIMARY KEY,
                    title TEXT NOT NULL,
                    recurrence_type INTEGER NOT NULL,
                    start_date TEXT NOT NULL,
                    interval_days INTEGER NULL,
                    days_of_week INTEGER NOT NULL DEFAULT 0,
                    day_of_month INTEGER NULL,
                    is_active INTEGER NOT NULL DEFAULT 1,
                    created_at_utc TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS routine_occurrences (
                    id TEXT PRIMARY KEY,
                    routine_id TEXT NOT NULL,
                    occurrence_date TEXT NOT NULL,
                    is_completed INTEGER NOT NULL DEFAULT 0,
                    completed_at_utc TEXT NULL,
                    UNIQUE (routine_id, occurrence_date),
                    FOREIGN KEY (routine_id) REFERENCES routines (id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS ix_occurrences_date ON routine_occurrences (occurrence_date);
                """;
            migration.ExecuteNonQuery();
        }

        if (version < 5 && _targetVersion >= 5)
        {
            // V0.5: projetos e entregas.
            //
            // As subtarefas são linhas de "tasks" com parent_id apontando para o
            // container. Isso preserva tudo o que já existe — ordem, conclusão,
            // datas — quando uma tarefa solta é movida para dentro de um projeto,
            // e garante estruturalmente o único nível de subtarefa: o pai nunca
            // pode ser outra tarefa.
            //
            // ON DELETE SET NULL: excluir um projeto devolve as subtarefas ao
            // estado de tarefa solta em vez de apagar trabalho do usuário.
            using var migration = connection.CreateCommand();
            migration.CommandText = """
                CREATE TABLE IF NOT EXISTS containers (
                    id TEXT PRIMARY KEY,
                    kind INTEGER NOT NULL,
                    title TEXT NOT NULL,
                    due_date TEXT NULL,
                    is_completed INTEGER NOT NULL DEFAULT 0,
                    completed_at_utc TEXT NULL,
                    sort_order INTEGER NOT NULL,
                    created_at_utc TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS ix_containers_kind ON containers (kind, sort_order);

                ALTER TABLE tasks ADD COLUMN parent_id TEXT NULL REFERENCES containers (id) ON DELETE SET NULL;
                ALTER TABLE tasks ADD COLUMN estimated_minutes INTEGER NULL;
                ALTER TABLE tasks ADD COLUMN can_split INTEGER NOT NULL DEFAULT 1;

                CREATE INDEX IF NOT EXISTS ix_tasks_parent ON tasks (parent_id);
                """;
            migration.ExecuteNonQuery();
        }

        if (version < 6 && _targetVersion >= 6)
        {
            // V0.6: provas. Tabelas novas; nada anterior é tocado.
            using var migration = connection.CreateCommand();
            migration.CommandText = """
                CREATE TABLE IF NOT EXISTS exams (
                    id TEXT PRIMARY KEY,
                    subject TEXT NOT NULL,
                    title TEXT NOT NULL,
                    exam_date TEXT NOT NULL,
                    notes TEXT NULL,
                    created_at_utc TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS exam_topics (
                    id TEXT PRIMARY KEY,
                    exam_id TEXT NOT NULL,
                    kind INTEGER NOT NULL DEFAULT 0,
                    title TEXT NOT NULL,
                    mastery INTEGER NOT NULL DEFAULT 0,
                    question_count INTEGER NULL,
                    estimated_minutes INTEGER NULL,
                    is_completed INTEGER NOT NULL DEFAULT 0,
                    is_active INTEGER NOT NULL DEFAULT 1,
                    sort_order INTEGER NOT NULL,
                    FOREIGN KEY (exam_id) REFERENCES exams (id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS ix_exam_topics_exam ON exam_topics (exam_id, sort_order);

                CREATE TABLE IF NOT EXISTS study_sessions (
                    id TEXT PRIMARY KEY,
                    exam_id TEXT NOT NULL,
                    topic_id TEXT NULL,
                    title TEXT NOT NULL,
                    estimated_minutes INTEGER NULL,
                    planned_date TEXT NULL,
                    can_split INTEGER NOT NULL DEFAULT 1,
                    is_completed INTEGER NOT NULL DEFAULT 0,
                    completed_at_utc TEXT NULL,
                    sort_order INTEGER NOT NULL,
                    created_at_utc TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL,
                    FOREIGN KEY (exam_id) REFERENCES exams (id) ON DELETE CASCADE,
                    FOREIGN KEY (topic_id) REFERENCES exam_topics (id) ON DELETE SET NULL
                );

                CREATE INDEX IF NOT EXISTS ix_sessions_planned ON study_sessions (planned_date);
                """;
            migration.ExecuteNonQuery();
        }

        if (version < 7 && _targetVersion >= 7)
        {
            // V0.7: prioridade, capacidade e plano do dia.
            //
            // A capacidade semanal é semeada com o mesmo valor que a V0.5 já usava
            // como padrão do distribuidor, para que o comportamento não mude no
            // upgrade. Nenhum dia nasce com 0: zerar um dia é uma decisão do usuário.
            using var migration = connection.CreateCommand();
            migration.CommandText = """
                ALTER TABLE tasks ADD COLUMN priority INTEGER NOT NULL DEFAULT 1;
                ALTER TABLE tasks ADD COLUMN manual_planning INTEGER NOT NULL DEFAULT 0;

                ALTER TABLE containers ADD COLUMN priority INTEGER NOT NULL DEFAULT 1;

                ALTER TABLE study_sessions ADD COLUMN priority INTEGER NOT NULL DEFAULT 1;
                ALTER TABLE study_sessions ADD COLUMN manual_planning INTEGER NOT NULL DEFAULT 0;

                CREATE TABLE IF NOT EXISTS weekly_capacity (
                    day_of_week INTEGER PRIMARY KEY,
                    minutes INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS capacity_overrides (
                    local_date TEXT PRIMARY KEY,
                    minutes INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS daily_plan (
                    local_date TEXT NOT NULL,
                    item_kind INTEGER NOT NULL,
                    item_id TEXT NOT NULL,
                    position INTEGER NOT NULL,
                    is_manual INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (local_date, item_kind, item_id)
                );

                CREATE INDEX IF NOT EXISTS ix_daily_plan_date ON daily_plan (local_date, position);

                INSERT OR IGNORE INTO weekly_capacity (day_of_week, minutes) VALUES
                    (0, 180), (1, 180), (2, 180), (3, 180), (4, 180), (5, 180), (6, 180);

                UPDATE tasks
                   SET manual_planning = 1
                 WHERE planned_date IS NOT NULL;
                """;
            migration.ExecuteNonQuery();
        }

        if (version < 8 && _targetVersion >= 8)
        {
            // V0.7.1: a árvore de estudo e o grafo de dependências.
            //
            // Cada exam_topic vira um study_node com o MESMO id. Isso é de
            // propósito: as sessões já existentes apontam para topic_id, então
            // copiar o id para node_id mantém todo o histórico ligado sem
            // precisar adivinhar nada.
            //
            // A hierarquia nasce toda na raiz e nenhuma dependência é criada:
            // não há como saber, a partir dos dados da V0.6, qual conteúdo é
            // pré-requisito de qual. Inventar isso travaria itens do usuário sem
            // ele ter pedido. A reorganização fica para as mãos dele.
            //
            // exam_topics não é apagada: ela fica como o retrato do que existia
            // antes desta migração.
            using var migration = connection.CreateCommand();
            migration.CommandText = """
                CREATE TABLE IF NOT EXISTS study_nodes (
                    id TEXT PRIMARY KEY,
                    exam_id TEXT NOT NULL,
                    parent_node_id TEXT NULL REFERENCES study_nodes (id) ON DELETE CASCADE,
                    node_type INTEGER NOT NULL DEFAULT 1,
                    title TEXT NOT NULL,
                    description TEXT NULL,
                    estimated_minutes INTEGER NULL,
                    can_split INTEGER NOT NULL DEFAULT 1,
                    mastery INTEGER NULL,
                    question_count INTEGER NULL,
                    sort_order INTEGER NOT NULL,
                    is_completed INTEGER NOT NULL DEFAULT 0,
                    completed_at_utc TEXT NULL,
                    created_at_utc TEXT NOT NULL,
                    FOREIGN KEY (exam_id) REFERENCES exams (id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS ix_study_nodes_exam ON study_nodes (exam_id, sort_order);
                CREATE INDEX IF NOT EXISTS ix_study_nodes_parent ON study_nodes (parent_node_id);

                CREATE TABLE IF NOT EXISTS study_dependencies (
                    id TEXT PRIMARY KEY,
                    exam_id TEXT NOT NULL,
                    node_id TEXT NOT NULL REFERENCES study_nodes (id) ON DELETE CASCADE,
                    depends_on_node_id TEXT NOT NULL REFERENCES study_nodes (id) ON DELETE CASCADE,
                    UNIQUE (node_id, depends_on_node_id),
                    FOREIGN KEY (exam_id) REFERENCES exams (id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS ix_study_dependencies_node ON study_dependencies (node_id);

                ALTER TABLE exams ADD COLUMN comfortable_daily_minutes INTEGER NOT NULL DEFAULT 120;
                ALTER TABLE exams ADD COLUMN max_daily_minutes INTEGER NOT NULL DEFAULT 180;
                ALTER TABLE exams ADD COLUMN max_session_minutes INTEGER NOT NULL DEFAULT 90;

                ALTER TABLE study_sessions ADD COLUMN node_id TEXT NULL;
                ALTER TABLE study_sessions ADD COLUMN needs_review INTEGER NOT NULL DEFAULT 0;

                INSERT OR IGNORE INTO study_nodes
                    (id, exam_id, parent_node_id, node_type, title, description, estimated_minutes,
                     can_split, mastery, question_count, sort_order, is_completed, completed_at_utc, created_at_utc)
                SELECT t.id,
                       t.exam_id,
                       NULL,
                       CASE t.kind WHEN 1 THEN 2 ELSE 1 END,
                       t.title,
                       NULL,
                       t.estimated_minutes,
                       1,
                       t.mastery,
                       t.question_count,
                       t.sort_order,
                       t.is_completed,
                       NULL,
                       e.created_at_utc
                  FROM exam_topics t
                  JOIN exams e ON e.id = t.exam_id
                 WHERE t.is_active = 1;

                UPDATE study_sessions SET node_id = topic_id WHERE topic_id IS NOT NULL;

                CREATE INDEX IF NOT EXISTS ix_sessions_node ON study_sessions (node_id);
                """;
            migration.ExecuteNonQuery();
        }

        if (version != _targetVersion)
        {
            using var stamp = connection.CreateCommand();
            stamp.CommandText = "DELETE FROM schema_info; INSERT INTO schema_info (version) VALUES ($version);";
            stamp.Parameters.AddWithValue("$version", _targetVersion);
            stamp.ExecuteNonQuery();
        }
    });

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _connection.Close();
            _connection.Dispose();
        }

        SqliteConnection.ClearAllPools();
    }
}
