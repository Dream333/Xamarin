﻿using System;
using System.Runtime.InteropServices;
using PepperSharp;

namespace MouseLock
{
    public class MouseLock : PPInstance
    {

        public MouseLock(IntPtr handle) : base(handle) { }

        ~MouseLock() { System.Console.WriteLine("MouseLock destructed"); }

        public override bool Init(int argc, string[] argn, string[] argv)
        {
            PPB_Console.Log(Instance, PP_LogLevel.Log, "Hello from MouseLock using C#");
            PPB_InputEvent.RequestInputEvents(Instance, (int)(PP_InputEvent_Class.Mouse | PP_InputEvent_Class.Keyboard));

            return true;
        }

        public override void DidChangeView(PP_Resource view)
        {
            var viewRect = new PP_Rect();
            var result = PPB_View.GetRect(view, out viewRect);

            // DidChangeView can get called for many reasons, so we only want to
            // rebuild the device context if we really need to.

            if ((size_.Width == viewRect.size.width && size_.Height == viewRect.size.height) &&
                (was_fullscreen_ == (PPB_View.IsFullscreen(view) == PP_Bool.PP_TRUE)) && 
                is_context_bound_)
            {
                Log($"DidChangeView SKIP {viewRect.size.width}, {viewRect.size.height} " +
                    $"FULL={(PPB_View.IsFullscreen(view) == PP_Bool.PP_TRUE)} " +
                    $"CTX Bound={is_context_bound_}");
                return;
            }

            Log($"DidChangeView DO {viewRect.size.width}, {viewRect.size.height} " +
    $"FULL={(PPB_View.IsFullscreen(view) == PP_Bool.PP_TRUE)} " +
    $"CTX Bound={is_context_bound_}");

            size_ = viewRect.size; ;
            device_context_ = PPB_Graphics2D.Create(Instance, size_, PP_Bool.PP_FALSE);
            waiting_for_flush_completion_ = false;

            is_context_bound_ = BindGraphics(device_context_);
            if (!is_context_bound_)
            {
                Log("Could not bind to 2D context\n.");
                return;
            }
            else
            {
                Log($"Bound to 2D context size {size_}.\n");
            }

            // Create a scanline for fill.
            background_scanline_ = new int[size_.Width];
            var bg_pixel = background_scanline_;
            for (int x = 0; x < size_.Width; ++x)
            {
                unchecked { bg_pixel[x] = (int)kBackgroundColor; };
            }

            // Remember if we are fullscreen or not
            was_fullscreen_ = (PPB_View.IsFullscreen(view) == PP_Bool.PP_TRUE);

            // Paint this context
            Paint();
        }

        public override void DidChangeFocus(bool hasFocus)
        {
            //Console.WriteLine($"Graphics_2D DidChangeFocus: {hasFocus}");
        }


        public override void MouseLockLost()
        {

            if (mouse_locked_)
            {
                Log("Mouselock unlocked.\n");
                mouse_locked_ = false;
                Paint();
            }
        }

        /// LockMouse() requests the mouse to be locked.
        ///
        /// While the mouse is locked, the cursor is implicitly hidden from the user.
        /// Any movement of the mouse will generate a
        /// <code>PP_InputEvent_Type.MOUSEMOVE</code> event. The
        /// <code>GetPosition()</code> function in <code>InputEvent()</code>
        /// reports the last known mouse position just as mouse lock was
        /// entered. The <code>GetMovement()</code> function provides relative
        /// movement information indicating what the change in position of the mouse
        /// would be had it not been locked.
        ///
        /// The browser may revoke the mouse lock for reasons including (but not
        /// limited to) the user pressing the ESC key, the user activating another
        /// program using a reserved keystroke (e.g. ALT+TAB), or some other system
        /// event.
        ///
        /// @param[in] cc A <code>CompletionCallback</code> to be called upon
        /// completion.
        ///
        /// @return An int32_t containing an error code from <code>pp_errors.h</code>.
        int LockMouse(PP_CompletionCallback cc)
        {
            Log("LockMouse");
            return PPB_MouseLock.LockMouse(Instance, cc);
        }

        void DidLockMouse(IntPtr userData, int result)
        {

            mouse_locked_ = (PP_Error)result == PP_Error.PP_OK;
            if ((PP_Error)result != PP_Error.PP_OK)
            {
                Log($"Mouselock failed with failed with error number {(PP_Error)result}.\n");
            }
            mouse_movement_ = PPPoint.Zero;
            Paint();

        }

        /// UnlockMouse causes the mouse to be unlocked, allowing it to track user
        /// movement again. This is an asynchronous operation. The module instance
        /// will be notified using the <code>PPP_MouseLock</code> interface when it
        /// has lost the mouse lock.
        void UnlockMouse()
        {
            PPB_MouseLock.UnlockMouse(Instance);
        }

        PP_Resource PaintImage(PPSize size)
        {
            
            var image = PPB_ImageData.Create(Instance, PP_ImageDataFormat.Bgra_premul, size, PP_Bool.PP_FALSE);

            if (image.pp_resource == 0) // || image.data() == NULL)
            {
                Log("Skipping image.\n");
                return image;
            }

            var desc = new PP_ImageDataDesc();

            if (PPB_ImageData.Describe(image, out desc) == PP_Bool.PP_FALSE)
            {
                Log("Skipping image.\n");
                return image;
            }

            ClearToBackground(image);

            DrawCenterSpot(image, kForegroundColor);
            DrawNeedle(image, kForegroundColor);
            return image;
        }

        void ClearToBackground(PP_Resource image)
        {
            if (image.pp_resource == 0)
            {
                Log("ClearToBackground with NULL image.");
                return;
            }
            if (background_scanline_ == null)
            {
                Log("ClearToBackground with no scanline.");
                return;
            }

            var desc = new PP_ImageDataDesc();

            if (PPB_ImageData.Describe(image, out desc) == PP_Bool.PP_FALSE)
            {
                return;
            }

            int[] data = null;
            IntPtr dataPtr = IntPtr.Zero;
            dataPtr = PPB_ImageData.Map(image);
            if (dataPtr == IntPtr.Zero)
                return;
            data = new int[(desc.size.width * desc.size.height)];

            Marshal.Copy(dataPtr, data, 0, data.Length);

            int image_height = desc.size.height; ;
            int image_width = desc.size.width; ;

            for (int y = 0; y < image_height; ++y)
            {
                Array.Copy(background_scanline_, 0, data, y * image_width, background_scanline_.Length);
            }

            Marshal.Copy(data, 0, dataPtr, data.Length);

        }

        void DrawCenterSpot(PP_Resource image,
                                       uint spot_color)
        {
            if (image.pp_resource == 0)
            {
                Log("DrawCenterSpot with NULL image");
                return;
            }

            var desc = new PP_ImageDataDesc();

            if (PPB_ImageData.Describe(image, out desc) == PP_Bool.PP_FALSE)
            {
                return;
            }

            int[] data = null;
            IntPtr dataPtr = IntPtr.Zero;
            dataPtr = PPB_ImageData.Map(image);
            if (dataPtr == IntPtr.Zero)
                return;
            data = new int[(desc.size.width * desc.size.height)];

            Marshal.Copy(dataPtr, data, 0, data.Length);

            // Draw the center spot.  The ROI is bounded by the size of the spot, plus
            // one pixel.
            int center_x = desc.size.width / 2;
            int center_y = desc.size.height / 2;
            int region_of_interest_radius = kCentralSpotRadius + 1;

            var left_top = new PPPoint(Math.Max(0, center_x - region_of_interest_radius),
                Math.Max(0, center_x - region_of_interest_radius));

            var right_bottom = new PPPoint(Math.Min(desc.size.width, center_x + region_of_interest_radius),
                Math.Min(desc.size.height, center_y + region_of_interest_radius));

            for (int y = left_top.Y; y < right_bottom.Y; ++y)
            {
                for (int x = left_top.X; x < right_bottom.X; ++x)
                {
                    if (MouseLock.GetDistance(x, y, center_x, center_y) < kCentralSpotRadius)
                    {
                        unchecked { data[y * desc.stride / 4 + x] = (int)spot_color; }
                    }
                }
            }

            Marshal.Copy(data, 0, dataPtr, data.Length);
            
        }

        void DrawNeedle(PP_Resource image,
                                   uint needle_color)
        {
            if (image.pp_resource == 0)
            {
                Log("DrawNeedle with NULL image");
                return;
            }

            var desc = new PP_ImageDataDesc();

            if (PPB_ImageData.Describe(image, out desc) == PP_Bool.PP_FALSE)
            {
                return;
            }

            int[] data = null;
            IntPtr dataPtr = IntPtr.Zero;
            dataPtr = PPB_ImageData.Map(image);
            if (dataPtr == IntPtr.Zero)
                return;
            data = new int[(desc.size.width * desc.size.height)];

            Marshal.Copy(dataPtr, data, 0, data.Length);

            if (GetDistance(mouse_movement_.X, mouse_movement_.Y, 0, 0) <=
                kCentralSpotRadius)
            {
                return;
            }

            int abs_mouse_x = Math.Abs(mouse_movement_.X);
            int abs_mouse_y = Math.Abs(mouse_movement_.Y);
            int center_x = desc.size.width / 2;
            int center_y = desc.size.height / 2;

            var vertex = new PPPoint(mouse_movement_.X + center_x,
                                    mouse_movement_.Y + center_y);

            var anchor_1 = new PPPoint();
            var anchor_2 = new PPPoint();

            
            MouseDirection direction = MouseDirection.Left;

            if (abs_mouse_x >= abs_mouse_y)
            {
                anchor_1.X = (center_x);
                anchor_1.Y = (center_y - kCentralSpotRadius);
                anchor_2.X = (center_x);
                anchor_2.Y = (center_y + kCentralSpotRadius);
                direction = (mouse_movement_.X < 0) ? MouseDirection.Left : MouseDirection.Right;
                if (direction == MouseDirection.Left)
                    anchor_1.Swap(ref anchor_2);
            }
            else
            {
                anchor_1.X = (center_x + kCentralSpotRadius);
                anchor_1.Y = (center_y);
                anchor_2.X = (center_x - kCentralSpotRadius);
                anchor_2.Y = (center_y);
                direction = (mouse_movement_.Y < 0) ? MouseDirection.Up : MouseDirection.Down;
                if (direction == MouseDirection.Up)
                    anchor_1.Swap(ref anchor_2);
            }

            var left_top = new PPPoint(Math.Max(0, center_x - abs_mouse_x),
                                        Math.Max(0, center_y - abs_mouse_y));

            var right_bottom = new PPPoint(Math.Min(desc.size.width, center_x + abs_mouse_x),
                                    Math.Min(desc.size.height, center_y + abs_mouse_y));

            for (int y = left_top.Y; y < right_bottom.Y; ++y)
            {
                for (int x = left_top.X; x < right_bottom.X; ++x)
                {
                    bool within_bound_1 = ((y - anchor_1.Y) * (vertex.X - anchor_1.X)) >
                      ((vertex.Y - anchor_1.Y) * (x - anchor_1.X));
                    bool within_bound_2 = ((y - anchor_2.Y) * (vertex.X - anchor_2.X)) <
                                          ((vertex.Y - anchor_2.Y) * (x - anchor_2.X));
                    bool within_bound_3 = (direction == MouseDirection.Up && y < center_y) ||
                                          (direction == MouseDirection.Down && y > center_y) ||
                                          (direction == MouseDirection.Left && x < center_x) ||
                                          (direction == MouseDirection.Right && x > center_x);

                    if (within_bound_1 && within_bound_2 && within_bound_3)
                    {
                        unchecked { data[y * desc.stride / 4 + x] = (int)needle_color; }
                    }
                }
            }

            Marshal.Copy(data, 0, dataPtr, data.Length);
        }

        // Return the Cartesian distance between two points.
        static float GetDistance(int point_1_x,
                           int point_1_y,
                           int point_2_x,
                           int point_2_y)
        {
            float v1 = point_1_x - point_2_x, v2 = point_1_y - point_2_y;
            return (float)Math.Sqrt((v1 * v1) + (v2 * v2));

        }

        void Paint()
        {

            // If we are already waiting to paint...
            if (waiting_for_flush_completion_)
            {
                return;
            }

            var image = PaintImage(size_);
            if (image.pp_resource == 0)
            {
                Log("Could not create image data\n");
                return;
            }

            PPB_Graphics2D.ReplaceContents(device_context_, image);
            waiting_for_flush_completion_ = true;

            var callDidFlush = new PP_CompletionCallback();
            callDidFlush.func = DidFlush;
            PPB_Graphics2D.Flush(device_context_, callDidFlush);
        }

        void DidFlush(IntPtr userData, int result)
        {
            if (result != 0)
                Log("Flushed failed with error number %d.\n", (PP_Error)result);
            waiting_for_flush_completion_ = false;
        }

        void Log(string format, params object[] args)
        {
            string message = string.Format(format, args);
            PPB_Console.Log(Instance, PP_LogLevel.Error, message);
        }

        PPSize size_ = PPSize.Zero;
        bool mouse_locked_;
        PPPoint mouse_movement_ = PPPoint.Zero;
        const int kCentralSpotRadius = 5;
        const uint kReturnKeyCode = 13;
        const uint kBackgroundColor = 0xff606060;
        const uint kForegroundColor = 0xfff08080;
        bool is_context_bound_;
        bool was_fullscreen_;
        int[] background_scanline_ = null;
        bool waiting_for_flush_completion_;

        // Indicate the direction of the mouse location relative to the center of the
        // view.  These values are used to determine which 2D quadrant the needle lies
        // in.
        enum MouseDirection {
            Left = 0,
            Right = 1,
            Up = 2,
            Down = 3
        }

        PP_Resource device_context_;


        public override bool HandleInputEvent(PP_Resource inputEvent)
        {

            var eventType = PPB_InputEvent.GetType(inputEvent);
            switch (eventType)
            {
                case PP_InputEvent_Type.Mousedown:

                    if (mouse_locked_)
                    {
                        UnlockMouse();
                    }
                    else
                    {
                        PP_CompletionCallback cc = new PP_CompletionCallback();
                        cc.func = DidLockMouse;
                        LockMouse(cc);

                    }
                    return true;


                case PP_InputEvent_Type.Mousemove:

                    if (PPB_MouseInputEvent.IsMouseInputEvent(inputEvent) == PP_Bool.PP_TRUE)
                    {
                        mouse_movement_ = PPB_MouseInputEvent.GetMovement(inputEvent);
                        Paint();
                        return true;
                    }

                    return false;


                case PP_InputEvent_Type.Keydown:

                    if (PPB_KeyboardInputEvent.IsKeyboardInputEvent(inputEvent) == PP_Bool.PP_TRUE)
                    {

                        // Switch in and out of fullscreen when 'Enter' is hit
                        if (PPB_KeyboardInputEvent.GetKeyCode(inputEvent) == kReturnKeyCode)
                        {
                            // Ignore switch if in transition
                            if (!is_context_bound_)
                                return true;

                            if (PPB_Fullscreen.IsFullscreen(Instance) == PP_Bool.PP_TRUE)
                            {
                                if (PPB_Fullscreen.SetFullscreen(Instance, PP_Bool.PP_FALSE) != PP_Bool.PP_TRUE)
                                {
                                    Log("Could not leave fullscreen mode\n");
                                }
                                else
                                {
                                    is_context_bound_ = false;
                                }
                            }
                            else
                            {
                                if (PPB_Fullscreen.SetFullscreen(Instance, PP_Bool.PP_TRUE) != PP_Bool.PP_TRUE)
                                {
                                    Log("Could not enter fullscreen mode\n");
                                }
                                else
                                {
                                    is_context_bound_ = false;
                                }
                            }

                        }
                        return true;
                    }
                    return false;


                case PP_InputEvent_Type.Mouseup:
                case PP_InputEvent_Type.Mouseenter:
                case PP_InputEvent_Type.Mouseleave:
                case PP_InputEvent_Type.Wheel:
                case PP_InputEvent_Type.Rawkeydown:
                case PP_InputEvent_Type.Keyup:
                case PP_InputEvent_Type.Char:
                case PP_InputEvent_Type.Contextmenu:
                case PP_InputEvent_Type.Ime_composition_start:
                case PP_InputEvent_Type.Ime_composition_update:
                case PP_InputEvent_Type.Ime_composition_end:
                case PP_InputEvent_Type.Ime_text:
                case PP_InputEvent_Type.Undefined:
                case PP_InputEvent_Type.Touchstart:
                case PP_InputEvent_Type.Touchmove:
                case PP_InputEvent_Type.Touchend:
                case PP_InputEvent_Type.Touchcancel:
                default:
                    return false;
            }

        }
    }
}