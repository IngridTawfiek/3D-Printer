/*
*************************************************************************************

  3D PROTOTYPING MACHINE CONTROLLER

  Program to control a machine to create a 3D resin-based prototype from a series of 
  CT scans

  Authors:
    Ingrid Tawfiek
    Michael Ramez
    Nadine Samy
    Bishoy Gamal

*************************************************************************************
*/


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kitware.VTK;
using System.Data;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace TryConsole
{
    class Program
    {
        private class PortAccess
        {
            // Call OutPut function from DLL file.
            [DllImport("inpout32.dll", EntryPoint = "Out32")]
            public static extern void Output(int adress, int value);

            //  Call Input functionfrom DLL file
            [DllImport("inpout32.dll", EntryPoint = "Inp32")]
            public static extern void Input(int adress);
        }

		// Main variables and parameters
        static vtkImageViewer2 _ImageViewer;
        static vtkTextMapper _SliceStatusMapper;
        static vtkRenderWindowInteractor iren;
        static int _Slice = 0;
        static int _SupportSlice = 0;
        static int X;
        static int Y;
        static double[] Z;
        static int _MinSlice = 0;
        static int _MaxSlice = 0;
        static int _ColorLevel = 0;
        static int _BlackLevel = 10000;
        static string path = "";
        static string SupportFile = "";
        static Timer timer1 = new Timer();
        static int _layertime = 600000; // Setting time for each resin layer (miliseconds)
        static int _LayerStep = 34;     // Number of steps to be moved by stepper motor for one layer (1 step = 0.03mm)
        static int _MaxLayer = 400;     // Maximum number of layers
        static int _MovedLayers = 0;    // Layers to initial position 
        static int _period = 4;         // Period for sending data to stepper motor (control stepper motor speed)
        static int _SupportLayers = 10; // Number of layers for Support 

		
        static void Down(int t, int layers)
        {
            for (int i = 0; i < layers; i++)
            {
                for (int j = 0; j < _LayerStep; j++)
                {
                    PortAccess.Output(888, 1); // 1 decimal = 0001 binary. This will set D0 to high
                    System.Threading.Thread.Sleep(t); // delay
                    PortAccess.Output(888, 3); // 2 decimal = 0010 binary. This will set D1 to high
                    System.Threading.Thread.Sleep(t); // delay
                    PortAccess.Output(888, 2); // 4 decimal = 0100 binary. This will set D2 to high
                    System.Threading.Thread.Sleep(t); // delay
                    PortAccess.Output(888, 0); // 8 decimal = 1000 binary. This will set D3 to high
                    System.Threading.Thread.Sleep(t); // delay
                }
            }
            _MovedLayers = _MovedLayers + layers;
        }

		
        static void Forward(int t, int layers)
        {
            for (int i = 0; i < layers; i++)
            {
                for (int j = 0; j < _LayerStep; j++)
                {
                    PortAccess.Output(888, 2); // 1 decimal = 0001 binary. This will set D0 to high
                    System.Threading.Thread.Sleep(t); // delay
                    PortAccess.Output(888, 3); // 2 decimal = 0010 binary. This will set D1 to high
                    System.Threading.Thread.Sleep(t); // delay
                    PortAccess.Output(888, 1); // 4 decimal = 0100 binary. This will set D2 to high
                    System.Threading.Thread.Sleep(t); // delay
                    PortAccess.Output(888, 0); // 8 decimal = 1000 binary. This will set D3 to high
                    System.Threading.Thread.Sleep(t); // delay
                }
            }
            _MovedLayers = _MovedLayers - layers;
        }

		
        static void Backward(int t, int layers)
        {
            _ImageViewer.SetColorLevel(_BlackLevel);
            _ImageViewer.Render();
            Down(_period, 10*layers );
            System.Threading.Thread.Sleep(5000);
            Forward (_period, 9*layers );
            System.Threading.Thread.Sleep(5000); // delay
            _ImageViewer.SetColorLevel(_ColorLevel );
            _ImageViewer.Render();
        }

		
        public static void InitTimer()
        {
            timer1.Tick += new EventHandler(timer1_Tick);
            timer1.Interval = _layertime; // miliseconds
            timer1.Start();
        }


        private static void timer1_Tick(object sender, EventArgs e)
        {
            if (_Slice < _MaxSlice) 
			{
				// Black screen
                _ImageViewer.SetColorLevel(_BlackLevel);
                _Slice++;
                _Slice++;
                _ImageViewer.SetSlice(_Slice);
                
				// Move stepper motor backwards
				Backward(_period, 1);
            } 
			else 
			{
                timer1.Stop();
				
                //Black screen
                _ImageViewer.SetColorLevel(_BlackLevel);
                _ImageViewer.Render();
                int x = _MaxLayer - _MovedLayers;
                Console.WriteLine(_MaxLayer);
                Console.WriteLine(_MovedLayers);
                Console.WriteLine(x);
                
				// Move stepper motor forwards
                Forward(_period, _MovedLayers);
            }
        }
		

        static void PrintImage(string Ipath)
        {
            vtkDICOMImageReader reader = vtkDICOMImageReader.New();
            reader.SetDirectoryName(Ipath);
            reader.Update();
            X = reader.GetWidth();
            Y = reader.GetHeight();
            Z = reader.GetPixelSpacing();
            Console.WriteLine(X * Z[0]);
            Console.WriteLine(Y * Z[1]);
            Console.WriteLine(Z[2]);

            // Visualize
            _ImageViewer = vtkImageViewer2.New();
            _ImageViewer.SetInputConnection(reader.GetOutputPort());
			
            // Get range of slices (min is the first index, max is the last index)
            _ImageViewer.GetSliceRange(ref _MinSlice, ref _MaxSlice);
            Console.WriteLine(_MinSlice);
            Console.WriteLine(_MaxSlice);

            _SliceStatusMapper = vtkTextMapper.New();
            _SliceStatusMapper.SetInputConnection(reader.GetOutputPort());
			
            vtkActor2D sliceStatusActor = vtkActor2D.New();
            sliceStatusActor.SetMapper(_SliceStatusMapper);

            vtkRenderWindow renderWindow = vtkRenderWindow.New();
            
			//Display in full screen
            renderWindow.SetFullScreen(1);

            vtkInteractorStyleImage interactorStyle = vtkInteractorStyleImage.New();
            renderWindow.GetRenderers().InitTraversal();
            vtkRenderer ren;
            while ((ren = renderWindow.GetRenderers().GetNextItem()) != null)
                renderWindow.AddRenderer(ren);

            _ImageViewer.SetRenderWindow(renderWindow);
            _ImageViewer.GetRenderer().AddActor2D(sliceStatusActor);
            _ImageViewer.SetSlice(_Slice);
            _ColorLevel = 500;
            _ImageViewer.SetColorLevel(_BlackLevel );
            _ImageViewer.Render();
            Down(_period, 62);
            Backward(_period, 1);

            _ImageViewer.SetColorLevel(_ColorLevel);
            _ImageViewer.Render();
            System.Threading.Thread.Sleep(_layertime ); // delay
			
            iren = vtkRenderWindowInteractor.New();
            iren.SetRenderWindow(renderWindow);
            renderWindow.Render();
			
            //Start Timer
            InitTimer();
            iren.Start();

            if (reader != null) reader.Dispose();
            if (_ImageViewer != null) _ImageViewer.Dispose();
            if (_SliceStatusMapper != null) _SliceStatusMapper.Dispose();
            if (sliceStatusActor != null) sliceStatusActor.Dispose();
            if (renderWindow != null) renderWindow.Dispose();
            if (interactorStyle != null) interactorStyle.Dispose();
            if (ren != null) ren.Dispose();
            if (iren != null) iren.Dispose();
        }

		
        static void ReadSupport(string Spath)
        {
            vtkJPEGReader reader = vtkJPEGReader.New();
            reader.SetFileName(Spath);
            reader.Update();

            // Visualize
            _ImageViewer = vtkImageViewer2.New();
            _ImageViewer.SetInputConnection(reader.GetOutputPort());
            
			_SliceStatusMapper = vtkTextMapper.New();
            _SliceStatusMapper.SetInputConnection(reader.GetOutputPort());
			
            vtkActor2D sliceStatusActor = vtkActor2D.New();
            sliceStatusActor.SetMapper(_SliceStatusMapper);
			
            vtkRenderWindow renderWindow = vtkRenderWindow.New();
            
			//Display in full screen
            renderWindow.SetFullScreen(1);

            vtkInteractorStyleImage interactorStyle = vtkInteractorStyleImage.New();
            renderWindow.GetRenderers().InitTraversal();
            vtkRenderer ren;
            while ((ren = renderWindow.GetRenderers().GetNextItem()) != null)
				renderWindow.AddRenderer(ren);

            _ImageViewer.SetRenderWindow(renderWindow);
            _ImageViewer.GetRenderer().AddActor2D(sliceStatusActor);
            _ImageViewer.SetSlice(_SupportSlice);

            iren = vtkRenderWindowInteractor.New();
            iren.SetRenderWindow(renderWindow);
            renderWindow.Render();
            _ColorLevel = 60;
            _ImageViewer.SetColorLevel(_BlackLevel);
            _ImageViewer.Render();
            Down(_period, 170);
            Backward(_period, 1);
          
            for (int i = 0; i < _SupportLayers; i++)
            {
                System.Threading.Thread.Sleep(_layertime); // delay
				Backward(_period , 1);
            }
            _ImageViewer.SetColorLevel(_BlackLevel);
            _ImageViewer.Render();
            Forward(_period, _MovedLayers);
            Console.WriteLine(_MovedLayers);

            if (reader != null) reader.Dispose();
            if (_ImageViewer != null) _ImageViewer.Dispose();
            if (_SliceStatusMapper != null) _SliceStatusMapper.Dispose();
            if (sliceStatusActor != null) sliceStatusActor.Dispose();
            if (renderWindow != null) renderWindow.Dispose();
            if (interactorStyle != null) interactorStyle.Dispose();
            if (ren != null) ren.Dispose();
            if (iren != null) iren.Dispose();
        }
		
		
		// Main routine
		// Takes as an argument the path to the directory containing the CT scans to print
        static void Main(string[] args)
        {
            path = args[0]; // path containing CT scan images
            PrintImage(path); // send control signalling to printer to create 3D model
        }

    }
}
