/* Copyright (c) 2016 Xamarin. */

/* NOTE: this is auto-generated from IDL */
/* From ppp_messaging.idl modified Thu May 12 07:00:02 2016. */

using System;
using System.Runtime.InteropServices;

namespace PepperSharp {


/**
 * @file
 * This file defines the PPP_Messaging interface containing pointers to
 * functions that you must implement to handle postMessage messages
 * on the associated DOM element.
 *
 */


/**
 * @addtogroup Interfaces
 * @{
 */
/**
 * The <code>PPP_Messaging</code> interface contains pointers to functions
 * that you must implement to handle postMessage events on the associated
 * DOM element.
 */
public static partial class PPPMessaging {
  [DllImport("PepperPlugin", EntryPoint = "PPP_Messaging_HandleMessage")]
  extern static void _HandleMessage ( PP_Instance instance,  PP_Var message);

  /**
   * HandleMessage() is a function that the browser calls when PostMessage()
   * is invoked on the DOM element for the module instance in JavaScript. Note
   * that PostMessage() in the JavaScript interface is asynchronous, meaning
   * JavaScript execution will not be blocked while HandleMessage() is
   * processing the message.
   *
   * @param[in] instance A <code>PP_Instance</code> identifying one instance
   * of a module.
   * @param[in] message A <code>PP_Var</code> which has been converted from a
   * JavaScript value. JavaScript array/object types are supported from Chrome
   * M29 onward. All JavaScript values are copied when passing them to the
   * plugin.
   *
   * When converting JavaScript arrays, any object properties whose name
   * is not an array index are ignored. When passing arrays and objects, the
   * entire reference graph will be converted and transferred. If the reference
   * graph has cycles, the message will not be sent and an error will be logged
   * to the console.
   *
   * The following JavaScript code invokes <code>HandleMessage</code>, passing
   * the module instance on which it was invoked, with <code>message</code>
   * being a string <code>PP_Var</code> containing "Hello world!"
   *
   * <strong>Example:</strong>
   *
   * @code
   *
   * <body>
   *   <object id="plugin"
   *           type="application/x-ppapi-postMessage-example"/>
   *   <script type="text/javascript">
   *     document.getElementById('plugin').postMessage("Hello world!");
   *   </script>
   * </body>
   *
   * @endcode
   *
   */
  public static void HandleMessage ( PP_Instance instance,  PP_Var message)
  {
  	 _HandleMessage (instance, message);
  }


}
/**
 * @}
 */


}
