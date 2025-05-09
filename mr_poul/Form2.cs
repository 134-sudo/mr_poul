using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace mr_poul
{
    public partial class Form2 : Form
    {
        // Объявляем переменные для работы с базой
        NpgsqlConnection conn;
        NpgsqlDataAdapter adapter;
        DataTable dt;
        // словарь для хранения данных для отображения в comboBox
        Dictionary<string, Dictionary<int, string>> lookupData = new Dictionary<string, Dictionary<int, string>>();

        public Form2()
        {
            InitializeComponent();
            string connection = "Host=localhost;Port=5432;Database=mr_poul;Username=postgres;Password=1111;";
            conn = new NpgsqlConnection(connection);
            // добавляем названия таблиц в comboBox, чтобы пользователь мог выбрать
            comboBox1.Items.AddRange(new object[] { "partner_products", "products", "partners", "material_types", "product_types", "materials", "users" });
            // обработчик смены выбранной таблицы
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
        }

        // Когда пользователь меняет выбор в comboBox
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            string table = comboBox1.Text; // получаем выбранную таблицу

            LoadLookupTables(); // загружаем связанные таблицы для отображения

            // создаем запрос для получения всех данных из выбранной таблицы
            string query = $"SELECT * FROM {table}";
            adapter = new NpgsqlDataAdapter(query, conn); 
            var builder = new NpgsqlCommandBuilder(adapter); 

            dt = new DataTable();
            adapter.Fill(dt);
            dataGridView1.Columns.Clear();
            dataGridView1.DataSource = dt;
            HidePrimaryKey(table);
            ReplaceForeignKeyColumns(table);
            dataGridView1.DefaultValuesNeeded -= dataGridView1_DefaultValuesNeeded;
            dataGridView1.DefaultValuesNeeded += dataGridView1_DefaultValuesNeeded;
        }

        // Кнопка "Сохранить" — сохраняет изменения в базу
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                adapter.Update(dt);
                MessageBox.Show("Данные успешно сохранены");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}");
            }
        }

        // Кнопка "Удалить" — удаляет выбранную строку
        private void button2_Click(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentRow != null && !dataGridView1.CurrentRow.IsNewRow)
            {
                dataGridView1.Rows.Remove(dataGridView1.CurrentRow);
                MessageBox.Show("Строка удалена. Сохраните изменения для обновления базы данных.");
            }
        }

        // Загружает связанные таблицы для отображения в ComboBox
        private void LoadLookupTables()
        {
            // загружаем словари для таблиц, связанных с FK
            lookupData["products"] = LoadLookup("products", "product_id", "product_name");
            lookupData["partners"] = LoadLookup("partners", "partner_id", "partner_name");
            lookupData["product_types"] = LoadLookup("product_types", "type_id", "product_type");
            lookupData["material_types"] = LoadLookup("material_types", "type_id", "material_type");
        }

        // Общий метод для загрузки данных из таблицы, возвращает словарь {ключ, значение}
        private Dictionary<int, string> LoadLookup(string table, string keyCol, string valCol)
        {
            var dict = new Dictionary<int, string>();
            using (var cmd = new NpgsqlCommand($"SELECT {keyCol}, {valCol} FROM {table}", conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dict[reader.GetInt32(0)] = reader.GetString(1);
                    }
                }
                conn.Close();
            }
            return dict;
        }

        // Скрывает первичный ключ, чтобы пользователь его не редактировал
        private void HidePrimaryKey(string table)
        {
            string pkColumn = null;

            // определяем название PK в зависимости от таблицы
            switch (table)
            {
                case "partner_products":
                    pkColumn = "sale_id";
                    break;
                case "products":
                    pkColumn = "product_id";
                    break;
                case "partners":
                    pkColumn = "partner_id";
                    break;
                case "material_types":
                    pkColumn = "type_id";
                    break;
                case "product_types":
                    pkColumn = "type_id";
                    break;
                case "materials":
                    pkColumn = "material_id";
                    break;
            }

            
            if (pkColumn != null && dataGridView1.Columns.Contains(pkColumn))
            {
                dataGridView1.Columns[pkColumn].Visible = false;
            }
        }

        // Заменяет FK-колонки на ComboBox для удобного выбора
        private void ReplaceForeignKeyColumns(string table)
        {
            switch (table)
            {
                case "partner_products":
                    ReplaceWithComboBox("product_id", "products");
                    ReplaceWithComboBox("partner_id", "partners");
                    break;
                case "products":
                    ReplaceWithComboBox("product_type_id", "product_types");
                    break;
                case "materials":
                    ReplaceWithComboBox("material_type_id", "material_types");
                    break;
            }
        }

        // Создает и вставляет ComboBox вместо FK-колонки
        private void ReplaceWithComboBox(string columnName, string lookupTable)
        {
            if (!dt.Columns.Contains(columnName))
                return;

            if (dataGridView1.Columns.Contains(columnName))
                dataGridView1.Columns.Remove(columnName);

            var combo = new DataGridViewComboBoxColumn
            {
                Name = columnName,
                DataPropertyName = columnName,
                HeaderText = columnName.Replace("_id", ""),
                DataSource = new BindingSource(lookupData[lookupTable], null),
                DisplayMember = "Value", 
                ValueMember = "Key",
                FlatStyle = FlatStyle.Flat
            };

            int index = dataGridView1.Columns.Count > 0 ? dataGridView1.Columns[columnName]?.Index ?? 0 : 0;
            dataGridView1.Columns.Insert(index, combo);
        }

        // Устанавливает значения по умолчанию при добавлении новой строки
        private void dataGridView1_DefaultValuesNeeded(object sender, DataGridViewRowEventArgs e)
        {
            string table = comboBox1.Text;
            switch (table)
            {
                case "partner_products":
                    SetDefaultValue(e.Row, "product_id", "products");
                    SetDefaultValue(e.Row, "partner_id", "partners");
                    e.Row.Cells["quantity"].Value = 1; // по умолчанию 1
                    e.Row.Cells["sale_date"].Value = DateTime.Now; // текущая дата
                    break;
                case "products":
                    SetDefaultValue(e.Row, "product_type_id", "product_types");
                    e.Row.Cells["min_partner_price"].Value = 0.0m; // цена по умолчанию
                    break;
                case "materials":
                    SetDefaultValue(e.Row, "material_type_id", "material_types");
                    e.Row.Cells["purchase_price"].Value = 0.0m;
                    e.Row.Cells["stock_quantity"].Value = 0;
                    break;
            }
        }

        // Устанавливает значение по умолчанию, выбирая первый ключ из словаря
        private void SetDefaultValue(DataGridViewRow row, string column, string lookupTable)
        {
            if (lookupData.ContainsKey(lookupTable) && lookupData[lookupTable].Any())
            {
                row.Cells[column].Value = lookupData[lookupTable].Keys.First();
            }
        }
    }
}