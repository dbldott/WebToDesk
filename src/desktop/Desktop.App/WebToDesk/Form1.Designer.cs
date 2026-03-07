
namespace WebToDesk;
partial class Form1
{

    private System.ComponentModel.IContainer components = null;


    private System.Windows.Forms.NotifyIcon notifyIcon1;


    private System.Windows.Forms.ContextMenuStrip trayMenu;

    // Метод освобождения ресурсов
    // Вызывается при уничтожении формы
    protected override void Dispose(bool disposing)
    {
        // Если освобождаем управляемые ресурсы
        if (disposing)
        {
            // Освобождаем иконку трея, если она создана.
            notifyIcon1?.Dispose();

            // Освобождаем контекстное меню, если оно создано.
            trayMenu?.Dispose();

            // Освобождаем контейнер компонентов, если он создан.
            components?.Dispose();
        }

        // Вызываем стандартный Dispose родительского класса.
        base.Dispose(disposing);
    }

    // Метод создаёт все визуальные компоненты формы.
    // WinForms обычно вызывает его из конструктора.
    private void InitializeComponent()
    {
        // Создаём контейнер компонентов.
        components = new System.ComponentModel.Container();

        // Создаём иконку в трее и передаём контейнер компонентов
        notifyIcon1 = new System.Windows.Forms.NotifyIcon(components);

        // Создаём контекстное меню трея
        trayMenu = new System.Windows.Forms.ContextMenuStrip(components);

        // Создаём пункт меню "Настройки".
        var settingsItem = new ToolStripMenuItem("Настройки");

        // Подписываемся на клик по пункту "Настройки".
        settingsItem.Click += SettingsItem_Click;

        // Создаём пункт меню "Выход".
        var exitItem = new ToolStripMenuItem("Выход");

        // Подписываемся на клик по пункту "Выход".
        exitItem.Click += ExitItem_Click;

        // Добавляем пункт "Настройки" в меню.
        trayMenu.Items.Add(settingsItem);

        // Добавляем разделитель между пунктами меню.
        trayMenu.Items.Add(new ToolStripSeparator());

        // Добавляем пункт "Выход" в меню.
        trayMenu.Items.Add(exitItem);

        // Привязываем созданное меню к иконке в трее.
        notifyIcon1.ContextMenuStrip = trayMenu;

        // Настройка масштабирования формы.
        AutoScaleMode = AutoScaleMode.Font;

        // Размер окна: ширина 800, высота 450.
        ClientSize = new Size(800, 450);

        // Заголовок окна.
        Text = "WebToDesk";

        // Текст-подсказка у иконки в трее.
        notifyIcon1.Text = "WebToDesk";

        // Делаем иконку в трее видимой.
        notifyIcon1.Visible = true;

        // Загружаем иконку из файла app.ico,
        // который должен лежать рядом с exe.
        notifyIcon1.Icon = new System.Drawing.Icon(
            System.IO.Path.Combine(AppContext.BaseDirectory, "app.ico")
        );
    }
}