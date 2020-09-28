using GongSolutions.Wpf.DragDrop;
using StellarisSQLiteModManager.Database;
using StellarisSQLiteModManager.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace StellarisSQLiteModManager.ViewModels
{
    public class MainPageViewModel : INotifyPropertyChanged
    {
        public MainPageViewModel()
        {
            AllMods = new Dictionary<string, Mod>();
            AllPlaysets = new ObservableCollection<Playset>();
            SelectedMods = new ObservableCollection<ModInPlayset>();
            UnselectedMods = new ObservableCollection<Mod>();

            SelectedDropHandler = new SelectedDropHandler(this);
            UnselectedDropHandler = new UnselectedDropHandler(this);

            DatabaseFunctions.Singleton.ReloadPlaysets(AllPlaysets);
            DatabaseFunctions.Singleton.ReloadAllMods(AllMods);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Dictionary<string, Mod> AllMods { get; }
        public ObservableCollection<ModInPlayset> SelectedMods { get; }
        public ObservableCollection<Mod> UnselectedMods { get; }
        public ObservableCollection<Playset> AllPlaysets { get; }
        public SelectedDropHandler SelectedDropHandler { get; }
        public UnselectedDropHandler UnselectedDropHandler { get; }

        private Playset selectedPlayset;
        public Playset SelectedPlayset
        {
            get
            {
                return selectedPlayset;
            }
            set
            {
                if(value != selectedPlayset)
                {
                    selectedPlayset = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(IsPlaysetSelected));
                    DatabaseFunctions.Singleton.ReloadPlaysetMods(AllMods, SelectedMods, UnselectedMods, SelectedPlayset);
                }
            }
        }

        public bool IsPlaysetSelected
        {
            get
            {
                if(SelectedPlayset != null)
                {
                    return true;
                }
                return false;
            }
        }

        public void CreatePlayset(string name)
        {
            DatabaseFunctions.Singleton.CreateNewPlayset(name);
            ReloadPlaysets();
        }

        public void ClonePlayset(string newName, Playset from)
        {
            DatabaseFunctions.Singleton.ClonePlaysetToNew(newName, from);
            ReloadPlaysets();
        }

        public void ReloadPlaysets()
        {
            DatabaseFunctions.Singleton.ReloadPlaysets(AllPlaysets);
        }

        public void ExportCurrentPlayset()
        {
            DatabaseFunctions.Singleton.ExportPlayset(SelectedPlayset);
        }

        public void ImportPlayset()
        {
            DatabaseFunctions.Singleton.ImportPlayset();
        }

    }

    public abstract class ModManagerDropHandler : IDropTarget
    {
        public ModManagerDropHandler(MainPageViewModel viewModel)
        {
            ViewModel = viewModel;
        }
        protected MainPageViewModel ViewModel { get; }

        public abstract void DragOver(IDropInfo dropInfo);

        public abstract void Drop(IDropInfo dropInfo);
    }

    public class SelectedDropHandler : ModManagerDropHandler
    {
        public SelectedDropHandler(MainPageViewModel viewModel) : base(viewModel)
        {
        }
        public override void DragOver(IDropInfo dropInfo)
        {
            Debug.WriteLine("DragOver Data: " + dropInfo.Data.GetType().FullName);

            bool ok = false;
            if (dropInfo.Data is ModInPlayset mip)
            {
                Debug.WriteLine("MIP");
                ok = true;
            }
            else if (dropInfo.Data is IList<object> mips)
            {
                if (mips.Count > 0 && mips[0] is ModInPlayset)
                {
                    Debug.WriteLine("MIPS");
                }
                else if (mips.Count > 0 && mips[0] is Mod)
                {
                    Debug.WriteLine("Mods");
                }
                ok = true;
            }
            else if (dropInfo.Data is Mod m)
            {
                Debug.WriteLine("Mod");
                ok = true;
            }

            if (ok)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public override void Drop(IDropInfo dropInfo)
        {
            Debug.WriteLine("Drop TargetItem: " + dropInfo.TargetItem.GetType().Name);
            Debug.WriteLine("Drop Data: " + dropInfo.Data.GetType().Name);

            Debug.WriteLine("RelativePosition: " + dropInfo.InsertPosition);
            Debug.WriteLine("InsertIndex: " + dropInfo.InsertIndex);

            if (dropInfo.Data is ModInPlayset mip)
            {
                int source = (int)mip.Index;
                int target = dropInfo.InsertIndex;

                MoveItemInSeleced(source, target);

                Debug.WriteLine("MIP");
            }
            else if (dropInfo.Data is IList<object> list)
            {
                if (list.Count > 0 && list[0] is ModInPlayset)
                {
                    int target = dropInfo.InsertIndex;
                    List<ModInPlayset> elements = new List<ModInPlayset>();

                    foreach (object item in list)
                    {
                        elements.Add((ModInPlayset)item);
                    }

                    elements.Sort((a, b) => { return a.Index.CompareTo(b.Index); });

                    foreach (ModInPlayset element in elements)
                    {
                        if (element.Index < target)
                        {
                            MoveItemInSeleced((int)element.Index, target);
                        }
                        else
                        {
                            //Items that get moved up would be prepended, increment target after each move
                            MoveItemInSeleced((int)element.Index, target++);
                        }
                    }
                }
                else if(list.Count > 0 && list[0] is Mod)
                {
                    int startIndex = dropInfo.InsertIndex;
                    int target = dropInfo.InsertIndex;
                    List<Mod> elements = new List<Mod>();

                    foreach (object item in list)
                    {
                        elements.Add((Mod)item);
                    }

                    foreach(Mod element in elements)
                    {
                        ModInPlayset newMip = new ModInPlayset(element, ViewModel.SelectedPlayset, target, true, false);
                        newMip.AddThis();
                        ViewModel.SelectedMods.Insert(target, newMip);
                        ViewModel.UnselectedMods.Remove(element);
                        target++;
                    }

                    for (int cnt = target; cnt < ViewModel.SelectedMods.Count; cnt++)
                    {
                        ViewModel.SelectedMods[cnt].Index += elements.Count;
                    }

                }
            }else if(dropInfo.Data is Mod mod)
            {
                int index = dropInfo.InsertIndex;
                ModInPlayset newMip = new ModInPlayset(mod, ViewModel.SelectedPlayset, index, true, false);
                newMip.AddThis();
                for(int cnt = index; cnt < ViewModel.SelectedMods.Count; cnt++)
                {
                    ViewModel.SelectedMods[cnt].Index++;
                }
                ViewModel.SelectedMods.Insert(index, newMip);
                ViewModel.UnselectedMods.Remove(mod);
            }
        }

        private void MoveItemInSeleced(int from, int to)
        {
            if (from < to)
            {
                if (--to > from)
                {
                    for (int cnt = from + 1; cnt <= to; cnt++)
                    {
                        ViewModel.SelectedMods[cnt].Index--;
                    }
                    ViewModel.SelectedMods[from].Index = to;
                    ViewModel.SelectedMods.Move(from, to);
                }
            }
            else
            {
                for (int cnt = from - 1; cnt >= to; cnt--)
                {
                    ViewModel.SelectedMods[cnt].Index++;
                }
                ViewModel.SelectedMods[from].Index = to;
                ViewModel.SelectedMods.Move(from, to);
            }
        }
    }

    public class UnselectedDropHandler : ModManagerDropHandler
    {
        public UnselectedDropHandler(MainPageViewModel viewModel) : base(viewModel)
        {
        }
        public override void DragOver(IDropInfo dropInfo)
        {
            Debug.WriteLine("DragOver Data: " + dropInfo.Data.GetType().FullName);

            bool ok = false;
            if (dropInfo.Data is ModInPlayset mip)
            {
                Debug.WriteLine("MIP");
                ok = true;
            }
            else if (dropInfo.Data is IList<object> mips)
            {
                if (mips.Count > 0 && mips[0] is ModInPlayset)
                {
                    Debug.WriteLine("MIPS");
                    ok = true;
                }
            }

            if (ok)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public override void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ModInPlayset mip)
            {
                ViewModel.UnselectedMods.Add(mip.TargetMod);
                ViewModel.SelectedMods.Remove(mip);
                mip.RemoveThis();

                for (int cnt = (int)mip.Index; cnt < ViewModel.SelectedMods.Count; cnt++)
                {
                    ViewModel.SelectedMods[cnt].Index = cnt;
                }
            }
            else if (dropInfo.Data is IList<object> list)
            {
                if (list.Count > 0 && list[0] is ModInPlayset)
                {
                    int target = dropInfo.InsertIndex;
                    List<ModInPlayset> elements = new List<ModInPlayset>();

                    foreach (object item in list)
                    {
                        elements.Add((ModInPlayset)item);
                    }

                    elements.Sort((a, b) => { return a.Index.CompareTo(b.Index); });
                    int first = (int)elements[0].Index;

                    foreach (ModInPlayset element in elements)
                    {
                        ViewModel.UnselectedMods.Add(element.TargetMod);
                        ViewModel.SelectedMods.Remove(element);
                        element.RemoveThis();
                    }

                    for(int cnt = first; cnt < ViewModel.SelectedMods.Count; cnt++)
                    {
                        ViewModel.SelectedMods[cnt].Index = cnt;
                    }
                }
            }
        }
    }
}
