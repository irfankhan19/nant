// NAnt - A .NET build tool
// Copyright (C) 2001-2002 Gerry Shaw
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

// Ian MacLean (ian@maclean.ms)
// Scott Hernandez (ScottHernandez@hotmail.com)

namespace SourceForge.NAnt {

    using System;
    using System.Collections;
    using System.Globalization;
    using System.Reflection;
    using System.Xml;

    using SourceForge.NAnt.Attributes;

    /// <summary>Models a NAnt XML element in the build file.</summary>
    /// <remarks>
    ///   <para>Automatically validates attributes in the element based on Attribute settings in the derived class.</para>
    /// </remarks>
    public class Element {
        #region Private Instance Fields

        private Location _location = Location.UnknownLocation;
        private Project _project = null;
        private XmlNode _xmlNode = null;
        private object _parent = null;
        #endregion Private Instance Fields        #region Private Static Fields
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion Private Static Fields

        #region Public Instance Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Element" /> class.
        /// </summary>
        public Element(){
        }

        #endregion Public Instance Constructors

        #region Protected Instance Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Element" /> class
        /// from the specified element.
        /// </summary>
        /// <param name="e">The element that should be used to create a new instance of the <see cref="Element" /> class.</param>
        protected Element(Element e) : this() {
            this._location = e._location;
            this._project = e._project;
            this._xmlNode = e._xmlNode;
        }

        #endregion Protected Instance Constructors

        #region Public Instance Properties

        /// <summary>
        /// Gets or sets the parent of the element.
        /// </summary>
        /// <value>
        /// The parent of the element.
        /// </value>
        /// <remarks>
        /// This will be the parent <see cref="Task" />, <see cref="Target" />, or 
        /// <see cref="Project" /> depending on where the element is defined.
        /// </remarks>
        public object Parent {
            get { return _parent; } 
            set { _parent = value; } 
        }

        /// <summary>
        /// Gets the name of the XML element used to initialize this element.
        /// </summary>
        /// <value>
        /// The name of the XML element used to initialize this element.
        /// </value>
        public virtual string Name {
            get {
                ElementNameAttribute elementNameAttribute = (ElementNameAttribute) 
                    Attribute.GetCustomAttribute(GetType(), typeof(ElementNameAttribute));

                string name = null;
                if (elementNameAttribute != null) {
                    name = elementNameAttribute.Name;
                }
                return name;
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="Project"/> to which this element belongs.
        /// </summary>
        /// <value>
        /// The <see cref="Project"/> to which this element belongs.
        /// </value>
        public virtual Project Project {
            get { return _project; }
            set { _project = value; }
        }

        /// <summary>
        /// Gets the properties local to this <see cref="Element" /> and the <see cref="Project" />.
        /// </summary>
        /// <value>
        /// The properties local to this <see cref="Element" /> and the <see cref="Project" />.
        /// </value>
        public virtual PropertyDictionary Properties {
            get { 
                return Project.Properties;
            }
        }

        #endregion Public Instance Properties

        #region Protected Instance Properties

        /// <summary>
        /// Gets or sets the xml node of the element.
        /// </summary>
        /// <value>
        /// The xml node of the element.
        /// </value>
        protected virtual XmlNode XmlNode {
            get { return _xmlNode; }
            set { _xmlNode = value; }
        }

        /// <summary>
        /// Gets or sets the location in the build file where the element is defined.
        /// </summary>
        /// <value>
        /// The location in the build file where the element is defined.
        /// </value>
        protected virtual Location Location {
            get { return _location; }
            set { _location = value; }
        }

        #endregion Protected Instance Properties

        #region Public Instance Methods            

        /// <summary>
        /// Performs default initialization.
        /// </summary>
        /// <remarks>
        /// <para>Derived classes that wish to add custom initialization should override 
        /// the <see cref="InitializeElement"/> method.
        /// </para>
        /// </remarks>
        public void Initialize(XmlNode elementNode) {
            if (Project == null) {
                throw new InvalidOperationException("Element has invalid Project property.");
            }

            // Save position in buildfile for reporting useful error messages.
            try {
                _location = Project.LocationMap.GetLocation(elementNode);
            }
            catch(ArgumentException ae) {
                Log.WriteLineIf(Project.Verbose, ae.ToString());
                //ignore
            }

            InitializeAttributes(elementNode);

            // Allow inherited classes a chance to do some custom initialization.
            InitializeElement(elementNode);
        }

        #endregion Public Instance Methods            

        #region Protected Instance Methods

        /// <summary>
        /// Derived classes should override to this method to provide extra initialization 
        /// and validation not covered by the base class.
        /// </summary>
        /// <param name="elementNode">The xml node of the element to use for initialization.</param>
        protected virtual void InitializeElement(XmlNode elementNode) {
        }

        #endregion Protected Instance Methods

        #region Private Instance Methods            

        /// <summary>
        /// Initializes all build attributes.
        /// </summary>
        private void InitializeAttributes(XmlNode elementNode) {
            // This is a bit of a monster function but if you look at it 
            // carefully this is what it does:            
            // * Looking for task attributes to initialize.
            // * For each BuildAttribute try to find the xml attribute that corresponds to it.
            // * Next process all the nested elements, same idea, look at what is supposed to
            //   be there from the attributes on the class/properties and then get
            //   the values from the xml node to set the instance properties.
            
            //* Removed the inheritance walking as it isn't necessary for extraction of public properties          
            _xmlNode = elementNode;

            Type currentType = GetType();
            
            PropertyInfo[] propertyInfoArray = currentType.GetProperties(BindingFlags.Public|BindingFlags.Instance);
            foreach (PropertyInfo propertyInfo in propertyInfoArray ) {
                // process all BuildAttribute attributes
                BuildAttributeAttribute buildAttribute = (BuildAttributeAttribute) 
                    Attribute.GetCustomAttribute(propertyInfo, typeof(BuildAttributeAttribute));

                if (buildAttribute != null) {
                    XmlNode attributeNode = elementNode.Attributes[buildAttribute.Name];

                    logger.Debug(string.Format(
                        CultureInfo.InvariantCulture,
                        "Found {0} <attribute> for {1}", 
                        buildAttribute.Name, 
                        propertyInfo.DeclaringType.FullName));

                    // check if its required
                    if (attributeNode == null && buildAttribute.Required) {
                        throw new BuildException(String.Format(CultureInfo.InvariantCulture, "'{0}' is a required attribute of <{1} ... \\>.", buildAttribute.Name, this.Name), Location);
                    }

                    if (attributeNode != null) {
                        string attrValue = attributeNode.Value;
                        if (buildAttribute.ExpandProperties) {
                            // expand attribute properites
                            attrValue = Project.ExpandProperties(attrValue, this.Location );
                            
                        }

                        logger.Debug(string.Format(
                            CultureInfo.InvariantCulture,
                            "Setting value: {3}.{0} = {2}({1})", 
                            buildAttribute.Name, 
                            attrValue,
                            attributeNode.Value,
                            propertyInfo.DeclaringType.Name));


                        if (propertyInfo.CanWrite) {
                            // set the property value instead
                            MethodInfo info = propertyInfo.GetSetMethod();
                            object[] paramaters = new object[1];

                            // If the object is an emum
                            Type propertyType = propertyInfo.PropertyType;

                            //validate attribute value with custom ValidatorAttribute(ors)
                            object[] validateAttributes = (ValidatorAttribute[]) 
                                Attribute.GetCustomAttributes(propertyInfo, typeof(ValidatorAttribute));
                            try {
                                foreach(ValidatorAttribute validator in validateAttributes) {
                                    logger.Info(string.Format(
                                        CultureInfo.InvariantCulture,
                                        "Validating Against {0}", 
                                        validator.GetType().ToString()));

                                    validator.Validate(attrValue);
                                }
                            } catch(ValidationException ve) {
                                logger.Error("Validation Exception", ve);
                                throw new ValidationException("Validation failed on" + propertyInfo.DeclaringType.FullName, Location, ve);
                            }
                            
                            //set paramaters[0] to value.
                            if (propertyType.IsSubclassOf(Type.GetType("System.Enum"))) {
                                try {
                                    paramaters[0] = Enum.Parse(propertyType, attrValue);
                                } catch (Exception) {
                                    // catch type conversion exceptions here
                                    string message = "Invalid value \"" + attrValue + "\". Valid values for this attribute are: ";
                                    foreach (object value in Enum.GetValues(propertyType)) {
                                        message += value.ToString() + ", ";
                                    }
                                    // strip last ,
                                    message = message.Substring(0, message.Length - 2);
                                    throw new BuildException(message, Location);
                                }
                            } else {
                                paramaters[0] = Convert.ChangeType(attrValue, propertyInfo.PropertyType, CultureInfo.InvariantCulture);
                            }
                            //set value
                            info.Invoke(this, paramaters);
                        }
                    }
                }

                // Do build Element Arrays ( assuming they are of a certain collection type.
                BuildElementArrayAttribute buildElementArrayAttribute = (BuildElementArrayAttribute) 
                    Attribute.GetCustomAttribute(propertyInfo, typeof(BuildElementArrayAttribute));
                if (buildElementArrayAttribute != null) {
                    
                    if(!propertyInfo.PropertyType.IsArray) {
                        throw new BuildException(String.Format(CultureInfo.InvariantCulture, " BuildElementArrayAttribute must be applied to array types '{0}' element for <{1} ...//>.", buildElementArrayAttribute.Name, this.Name), Location);
                    }
                    
                    // get collection of nodes  ( TODO - do this without using xpath )
                    XmlNodeList nodes  = elementNode.SelectNodes( "nant:" + buildElementArrayAttribute.Name, Project.NamespaceManager);

                    // check if its required
                    if (nodes == null && buildElementArrayAttribute.Required) {
                        throw new BuildException(String.Format(CultureInfo.InvariantCulture, " Element Required! There must be a least one '{0}' element for <{1} ...//>.", buildElementArrayAttribute.Name, this.Name), Location);
                    }

                    // get the type of the array elements
                    Type elementType = propertyInfo.PropertyType.GetElementType();
                    // create new array of the required size - even if size is 0
                    System.Array list = Array.CreateInstance(elementType, nodes.Count);

                    int arrayIndex =0;
                    foreach ( XmlNode childNode in nodes ) {
                        // Create a child element
                        Element childElement = (Element) Activator.CreateInstance(elementType); 
                        
                        childElement.Project = Project;
                        childElement.Initialize(childNode);
                        list.SetValue(childElement, arrayIndex);
                        arrayIndex ++;
                    }
                    
                    // set the memvber array to our newly created array
                    propertyInfo.SetValue(this, list, null);
                }
                // now do nested BuildElements
                BuildElementAttribute buildElementAttribute = (BuildElementAttribute) 
                    Attribute.GetCustomAttribute(propertyInfo, typeof(BuildElementAttribute));

                if (buildElementAttribute != null && buildElementArrayAttribute == null ) { // if we're not an array element either
                    // get value from xml node
                    XmlNode nestedElementNode = elementNode[buildElementAttribute.Name, elementNode.OwnerDocument.DocumentElement.NamespaceURI]; 
                    // check if its required
                    if (nestedElementNode == null && buildElementAttribute.Required) {
                        throw new BuildException(String.Format(CultureInfo.InvariantCulture, "'{0}' is a required element of <{1} ...//>.", buildElementAttribute.Name, this.Name), Location);
                    }
                    if (nestedElementNode != null) {
                        Element childElement = (Element)propertyInfo.GetValue(this, null);
                        // Sanity check: Ensure property wasn't null.
                        if ( childElement == null )
                            throw new BuildException(String.Format(CultureInfo.InvariantCulture, "Property '{0}' value cannot be null for <{1} ...//>", propertyInfo.Name, this.Name), Location);
                        childElement.Project = Project;
                        childElement.Initialize(nestedElementNode);
                    }                        
                }
            }            
        }

        #endregion Private Instance Methods            
    }
}
